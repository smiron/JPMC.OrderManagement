using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using JPMC.OrderManagement.API.Services.Interfaces;

namespace JPMC.OrderManagement.API.Services;

public class OrderManager(IDynamoDBContext dynamoDbContext) : IOrderManager
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

    public async Task<ApiModels.Order?> GetOrder(int orderId)
    {
        var order = await dynamoDbContext.LoadAsync<DataModels.Order>($"ORDER#{orderId}", $"ORDER#{orderId}");

        return order == null
            ? null
            : new ApiModels.Order
            {
                Id = orderId,
                Symbol = order.Symbol,
                Side = order.Side,
                Amount = order.Amount,
                Price = order.Price
            };
    }

    public async Task AddOrder(int orderId, string symbol, ApiModels.Side side, int amount, int price)
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

        var createOrderDocument = dynamoDbContext.ToDocument(order);

        try
        {
            await dynamoDbContext.GetTargetTable<DataModels.Order>().PutItemAsync(
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
            var updatedOrder = new DataModels.Order
            {
                Pk = $"ORDER#{orderId}",
                Sk = $"ORDER#{orderId}",
                Id = orderId.ToString(),
                Amount = amount,
                Price = price
            };

            var updateOrderDocument = new Document
            {
                { DataModels.Attributes.Pk, updatedOrder.Pk },
                { DataModels.Attributes.Sk, updatedOrder.Sk },
                { DataModels.Attributes.Id, updatedOrder.Id },
                { DataModels.Attributes.Amount, updatedOrder.Amount },
                { DataModels.Attributes.Price, updatedOrder.Price },
                { DataModels.Attributes.ETag, updatedOrder.ETag },
            };

            await dynamoDbContext.GetTargetTable<DataModels.Order>().UpdateItemAsync(
                updateOrderDocument, 
                _updateItemOperationConfig);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new OrderManagerException("Order does not exist.");
        }
    }
}

public class OrderManagerException(string message) : Exception(message);