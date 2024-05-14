using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using JPMC.OrderManagement.API.ApiModels;
using JPMC.OrderManagement.API.Services.Interfaces;

namespace JPMC.OrderManagement.API.Services;

public class OrderManager(IDynamoDBContext dynamoDbContext, DynamoDBOperationConfig dynamoDbOperationConfig) : IOrderManager
{
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
        var order = await dynamoDbContext.LoadAsync<DataModels.Order>($"ORDER#{orderId}", $"ORDER#{orderId}", dynamoDbOperationConfig);

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
        var order = new DataModels.Order
        {
            Id = orderId.ToString(),
            Symbol = symbol,
            Side = side,
            Amount = amount,
            Price = price,
            EntityType = "ORDER",
            Pk = $"ORDER#{orderId}",
            Sk = $"ORDER#{orderId}"
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
            var orderToUpdate = new DataModels.Order
            {
                Pk = $"ORDER#{orderId}",
                Sk = $"ORDER#{orderId}",
                Id = orderId.ToString(),
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

        var orderToDelete = new DataModels.Order
        {
            Pk = $"ORDER#{orderId}",
            Sk = $"ORDER#{orderId}"
        };

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

    public async Task<int> CalculatePrice(string symbol, Side side, int amount)
    {
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
                    IndexName = "GSI1"
                });

        int fulfilledAmount = 0;
        int calculatedPrice = 0;

        // Iterate as long as there are more orders to go through, and we've not fulfilled the trade amount.
        while (!orderQuery.IsDone && fulfilledAmount < amount)
        {
            var ordersPage = await orderQuery.GetNextSetAsync();

            foreach (var order in ordersPage)
            {
                int orderFulfilledAmount = amount - fulfilledAmount > order.Amount ? order.Amount : amount - fulfilledAmount;
                calculatedPrice += orderFulfilledAmount * order.Price;
                fulfilledAmount += orderFulfilledAmount;

                if (fulfilledAmount >= amount)
                {
                    // End the order processing loop early if we've already fulfilled the trade amount
                    break;
                }
            }
        }

        if (fulfilledAmount < amount)
        {
            throw new OrderManagerException("The order book doesn't have enough orders to satisfy the required trade amount. Please retry at a later time.");
        }

        return calculatedPrice;
    }

    public async Task PlaceTrade(string symbol, Side side, int amount)
    {
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
                    IndexName = "GSI1"
                });

        var ordersToUpdate = new List<DataModels.Order>();
        int fulfilledAmount = 0;

        // Iterate as long as there are more orders to go through, and we've not fulfilled the trade amount.
        while (!orderQuery.IsDone && fulfilledAmount < amount)
        {
            var ordersPage = await orderQuery.GetNextSetAsync();

            foreach (var order in ordersPage)
            {
                int orderFulfilledAmount = amount - fulfilledAmount > order.Amount ? order.Amount : amount - fulfilledAmount;
                fulfilledAmount += orderFulfilledAmount;
                order.Amount -= orderFulfilledAmount;
                ordersToUpdate.Add(order);

                if (fulfilledAmount >= amount)
                {
                    // End the order processing loop early if we've already fulfilled the trade amount
                    break;
                }
            }
        }

        if (fulfilledAmount < amount)
        {
            throw new OrderManagerException("The order book doesn't have enough orders to satisfy the required trade amount. Please retry at a later time.");
        }

        var tradesDocumentTransactWrite = dynamoDbContext.GetTargetTable<DataModels.Trade>(dynamoDbOperationConfig).CreateTransactWrite();
        var ordersDocumentTransactWrite = dynamoDbContext.GetTargetTable<DataModels.Order>(dynamoDbOperationConfig).CreateTransactWrite();

        var tradeId = Guid.NewGuid().ToString("D");
        tradesDocumentTransactWrite.AddDocumentToPut(dynamoDbContext.ToDocument(new DataModels.Trade
            {
                Pk = $"TRADE#{tradeId}",
                Sk = $"TRADE#{tradeId}",
                Amount = amount,
                EntityType = "TRADE",
                Id = tradeId,
                Side = side,
                Symbol = symbol
            },
            dynamoDbOperationConfig));

        foreach (var order in ordersToUpdate)
        {
            if (order.Amount > 0)
            {
                var updateConfig = new TransactWriteItemOperationConfig
                {
                    ConditionalExpression = new Expression
                    {
                        ExpressionStatement = "ETag = :currentETag",
                        ExpressionAttributeValues =
                        {
                            [":currentETag"] = new Primitive(order.ETag)
                        }
                    }
                };

                ordersDocumentTransactWrite.AddDocumentToUpdate(
                    dynamoDbContext.ToDocument(order, dynamoDbOperationConfig),
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
                            [":currentETag"] = new Primitive(order.ETag)
                        }
                    }
                };

                ordersDocumentTransactWrite.AddItemToDelete(
                    dynamoDbContext.ToDocument(order, dynamoDbOperationConfig),
                    deleteConfig);
            }
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
}

public class OrderManagerException(string message) : Exception(message);