using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.Common.Models;

namespace JPMC.OrderManagement.API.DataModels;

internal class Trade() : EntityBase(DynamoDbEntityTypes.Trade)
{
    public Trade(string tradeId) : this()
    {
        Id = tradeId;
        Pk = $"{DynamoDbEntityTypes.Trade}#{tradeId}";
        Sk = $"{DynamoDbEntityTypes.Trade}#{tradeId}";
    }

    [DynamoDBProperty(DynamoDbAttributes.Symbol)] public string Symbol { get; set; } = null!;

    [DynamoDBProperty(DynamoDbAttributes.Side)] public Side Side { get; set; }

    [DynamoDBProperty(DynamoDbAttributes.Amount)] public int Amount { get; set; }
}