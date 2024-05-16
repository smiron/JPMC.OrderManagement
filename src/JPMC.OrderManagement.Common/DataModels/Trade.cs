using Amazon.DynamoDBv2.DataModel;

namespace JPMC.OrderManagement.Common.DataModels;

public class Trade() : EntityBase(DynamoDbEntityTypes.Trade)
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