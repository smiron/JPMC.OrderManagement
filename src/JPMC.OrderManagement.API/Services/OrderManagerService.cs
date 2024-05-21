using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using JPMC.OrderManagement.API.Options;
using JPMC.OrderManagement.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using DataModels = JPMC.OrderManagement.Common.DataModels;
using Order = JPMC.OrderManagement.API.ApiModels.Order;
using Side = JPMC.OrderManagement.Common.DataModels.Side;

namespace JPMC.OrderManagement.API.Services;

internal class OrderManagerService(
    IDynamoDBContext dynamoDbContext, 
    IAmazonS3 s3Client, 
    IOptions<ServiceOptions> serviceOptions,
    DynamoDBOperationConfig dynamoDbOperationConfig) : IOrderManagerService
{
    private const string Gsi1IndexName = "GSI1";

    private readonly PutItemOperationConfig _createItemOperationConfig = new()
    {
        ConditionalExpression = new Expression
        {
            ExpressionStatement = "attribute_not_exists(ID)"
        }
    };

    private readonly UpdateItemOperationConfig _updateItemOperationConfig = new()
    {
        ConditionalExpression = new Expression
        {
            ExpressionStatement = "attribute_exists(ID)"
        }
    };

    private readonly DeleteItemOperationConfig _deleteItemOperationConfig = new()
    {
        ConditionalExpression = new Expression
        {
            ExpressionStatement = "attribute_exists(ID)"
        }
    };

    public async Task<Order?> GetOrder(int orderId)
    {
        // The initial value for this variable is only used to calculate the DynamoDB record PK and SK.
        // We subsequently override the variable value with the actual record retrieved from DynamoDB
        var order = new DataModels.Order(orderId);
        order = await dynamoDbContext.LoadAsync<DataModels.Order>(order.Pk, order.Sk, dynamoDbOperationConfig);

        return order == null
            ? null
            : new Order
            {
                Id = orderId,
                Symbol = order.Symbol,
                Side = order.Side,
                Amount = order.Amount,
                Price = order.Price
            };
    }

    public async Task AddOrder(int orderId, string symbol, Side side, int amount, int price)
    {
        var order = new DataModels.Order(orderId)
        {
            Symbol = symbol,
            Side = side,
            Amount = amount,
            Price = price
        };

        var createOrderDocument = dynamoDbContext.ToDocument(order, dynamoDbOperationConfig);

        try
        {
            await dynamoDbContext.GetTargetTable<DataModels.Order>(dynamoDbOperationConfig)
                .PutItemAsync(
                    createOrderDocument,
                    _createItemOperationConfig);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new OrderManagerException("An Order with the same ID already exists.");
        }
    }

    public async Task ModifyOrder(int orderId, int amount, int price)
    {
        try
        {
            var orderToUpdate = new DataModels.Order(orderId)
            {
                Amount = amount,
                Price = price
            };

            var updateOrderDocument = new Document
            {
                { DataModels.DynamoDbAttributes.Pk, orderToUpdate.Pk },
                { DataModels.DynamoDbAttributes.Sk, orderToUpdate.Sk },
                { DataModels.DynamoDbAttributes.Id, orderToUpdate.Id },
                { DataModels.DynamoDbAttributes.Amount, orderToUpdate.Amount },
                { DataModels.DynamoDbAttributes.Price, orderToUpdate.Price },
                { DataModels.DynamoDbAttributes.ETag, orderToUpdate.ETag },
            };

            await dynamoDbContext.GetTargetTable<DataModels.Order>(dynamoDbOperationConfig)
                .UpdateItemAsync(
                    updateOrderDocument,
                    _updateItemOperationConfig);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new OrderManagerException("Order does not exist.");
        }
    }

    public async Task RemoveOrder(int orderId)
    {

        var orderToDelete = new DataModels.Order(orderId);

        var orderToDeleteDocument = new Document
        {
            { DataModels.DynamoDbAttributes.Pk, orderToDelete.Pk },
            { DataModels.DynamoDbAttributes.Sk, orderToDelete.Sk }
        };

        try
        {
            await dynamoDbContext.GetTargetTable<DataModels.Order>(dynamoDbOperationConfig)
                .DeleteItemAsync(
                    orderToDeleteDocument,
                    _deleteItemOperationConfig);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new OrderManagerException("Order does not exist.");
        }
    }

    public async Task<OrderBatchLoad> BatchLoad()
    {
        var creationTimestamp = DateTime.UtcNow;
        var expirationTimestamp = creationTimestamp.AddDays(1);

        var preSignedUrl = await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = serviceOptions.Value.BatchLoadingS3Bucket,
            Expires = expirationTimestamp,
            Key = $"batch-load/{creationTimestamp:yyyyMMddHHmmss}-{Guid.NewGuid():D}.csv",
            ContentType = "text/csv",
            Headers = { ContentType = "text/csv" },
            Protocol = Protocol.HTTPS
        });

        return new OrderBatchLoad
        {
            CreationTimestampUtc = creationTimestamp,
            ExpirationTimestampUtc = expirationTimestamp,
            PreSignedUrl = preSignedUrl
        };
    }

    public async Task<int> CalculatePrice(string symbol, Side side, int amount)
    {
        int fulfilledAmount = 0;
        int calculatedPrice = 0;

        await foreach (var tradeOrder in GetTradeOrders(symbol, side, amount))
        {
            calculatedPrice += tradeOrder.OrderFulfilledAmount * tradeOrder.Order.Price;
            fulfilledAmount = tradeOrder.TotalFulfilledAmount;
        }

        if (fulfilledAmount < amount)
        {
            throw new OrderManagerException("The order book doesn't have enough orders to satisfy the required trade amount. Please retry at a later time.");
        }

        return calculatedPrice;
    }

    /// <summary>
    /// Trade placement functionality - When a trade is placed, the application subtracts the amount bought or sold to the client from each order.
    /// Empty orders, where amount is equal to zero, are removed from the order book.
    /// NOTE: A transaction is being used to make the data changes.
    /// </summary>
    /// <param name="symbol">The trade symbol</param>
    /// <param name="side">The trade side</param>
    /// <param name="amount">The trade amount</param>
    /// <returns>An async Task for placing the trade.</returns>
    /// <exception cref="OrderManagerException">
    /// Exceptions are thrown if the order book doesn't contain enough orders to fulfill the trade amount or if the underlying data has changed during the trade placement.
    /// </exception>
    public async Task PlaceTrade(string symbol, Side side, int amount)
    {
        var tradesDocumentTransactWrite = dynamoDbContext.GetTargetTable<DataModels.Trade>(dynamoDbOperationConfig).CreateTransactWrite();
        var ordersDocumentTransactWrite = dynamoDbContext.GetTargetTable<DataModels.Order>(dynamoDbOperationConfig).CreateTransactWrite();

        var tradeId = Guid.NewGuid().ToString("D");
        tradesDocumentTransactWrite.AddDocumentToPut(dynamoDbContext.ToDocument(new DataModels.Trade(tradeId)
{
                Amount = amount,
                Side = side,
                Symbol = symbol
            },
            dynamoDbOperationConfig));

        int fulfilledAmount = 0;

        await foreach (var tradeOrder in GetTradeOrders(symbol, side, amount))
        {
            tradeOrder.Order.Amount -= tradeOrder.OrderFulfilledAmount;
            fulfilledAmount = tradeOrder.TotalFulfilledAmount;

            if (tradeOrder.Order.Amount > 0)
            {
                var updateConfig = new TransactWriteItemOperationConfig
                {
                    ConditionalExpression = new Expression
                    {
                        ExpressionStatement = "ETag = :currentETag",
                        ExpressionAttributeValues =
                        {
                            [":currentETag"] = new Primitive(tradeOrder.Order.ETag)
                        }
                    }
                };

                ordersDocumentTransactWrite.AddDocumentToUpdate(
                    dynamoDbContext.ToDocument(tradeOrder.Order, dynamoDbOperationConfig),
                    updateConfig);
            }
            else
            {
                var deleteConfig = new TransactWriteItemOperationConfig
                {
                    ConditionalExpression = new Expression
                    {
                        ExpressionStatement = "ETag = :currentETag",
                        ExpressionAttributeValues =
                        {
                            [":currentETag"] = new Primitive(tradeOrder.Order.ETag)
                        }
                    }
                };

                ordersDocumentTransactWrite.AddItemToDelete(
                    dynamoDbContext.ToDocument(tradeOrder.Order, dynamoDbOperationConfig),
                    deleteConfig);
            }
        }

        if (fulfilledAmount < amount)
        {
            throw new OrderManagerException("The order book doesn't have enough orders to satisfy the required trade amount. Please retry at a later time.");
        }

        try
        {
            await tradesDocumentTransactWrite.Combine(ordersDocumentTransactWrite).ExecuteAsync();
        }
        catch (TransactionCanceledException)
        {
            throw new OrderManagerException("The orders due to be used in this trade have been updated. Please retry.");
        }
    }

    private async IAsyncEnumerable<(DataModels.Order Order, int OrderFulfilledAmount, int TotalFulfilledAmount)> GetTradeOrders(string symbol, Side side, int amount)
    {
        int fulfilledAmount = 0;

        var orderQuery =
            dynamoDbContext.QueryAsync<DataModels.Order>(
                $"{symbol}#{side}",
                new DynamoDBOperationConfig
                {
                    TableNamePrefix = dynamoDbOperationConfig.TableNamePrefix,
                    OverrideTableName = dynamoDbOperationConfig.OverrideTableName,
                    SkipVersionCheck = dynamoDbOperationConfig.SkipVersionCheck,
                    // The best Buy price is the one of the order with the smallest price in the book -> traverse the index forward
                    // The best Sell price is the one of the order with the highest price in the book -> traverse the index backward
                    BackwardQuery = side == Side.Sell,
                    IndexName = Gsi1IndexName
                });

        // Iterate as long as there are more orders to go through, and we've not fulfilled the trade amount.
        while (!orderQuery.IsDone && fulfilledAmount < amount)
        {
            var ordersPage = await orderQuery.GetNextSetAsync();

            foreach (var order in ordersPage)
            {
                int orderFulfilledAmount = amount - fulfilledAmount > order.Amount ? order.Amount : amount - fulfilledAmount;
                fulfilledAmount += orderFulfilledAmount;

                yield return new ValueTuple<DataModels.Order, int, int>(order, orderFulfilledAmount, fulfilledAmount);

                if (fulfilledAmount >= amount)
                {
                    // End the order processing loop early if we've already fulfilled the trade amount
                    yield break;
                }
            }
        }
    }
}

public class OrderManagerException(string message) : Exception(message);