using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

[DynamoDBTable("jpmc.ordermanagement")]
public class Trade
{
    [DynamoDBHashKey("PK")] public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("SK")] public string Sk { get; set; } = null!;

    [DynamoDBProperty] public string EntityType { get; set; } = null!;

    [DynamoDBProperty] public DateTime CreateTimestamp { get; set; } = DateTime.UtcNow;

    [DynamoDBProperty] public DateTime? UpdateTimestamp { get; set; }

    [DynamoDBProperty("ID")] public int Id { get; set; }

    [DynamoDBProperty] public string Symbol { get; set; } = null!;

    [DynamoDBProperty] public Side Side { get; set; }

    [DynamoDBProperty] public int Amount { get; set; }
}