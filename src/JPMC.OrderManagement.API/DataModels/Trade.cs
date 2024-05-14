using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

public class Trade() : EntityBase(TradeEntityType)
{
    public const string TradeEntityType = "TRADE";

    public Trade(string tradeId) : this()
    {
        Id = tradeId;
        Pk = $"{TradeEntityType}#{tradeId}";
        Sk = $"{TradeEntityType}#{tradeId}";
    }

    [DynamoDBProperty(DynamoDbAttributes.Symbol)] public string Symbol { get; set; } = null!;

    [DynamoDBProperty(DynamoDbAttributes.Side)] public Side Side { get; set; }

    [DynamoDBProperty(DynamoDbAttributes.Amount)] public int Amount { get; set; }
}