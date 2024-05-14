using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

// TODO: inject the table name dynamically
[DynamoDBTable("jpmc.ordermanagement")]
public record Order
{
    [DynamoDBHashKey("PK")] public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("SK")] public string Sk { get; set; } = null!;

    [DynamoDBProperty] public string EntityType { get; set; } = null!;

    [DynamoDBProperty] public DateTime CreateTimestamp { get; set; } = DateTime.UtcNow;

    [DynamoDBProperty] public DateTime? UpdateTimestamp { get; set; }

    [DynamoDBProperty("ID")] public int Id { get; set; }

    [DynamoDBProperty] public string Symbol { get; set; } = null!;

    [DynamoDBProperty] public Side Side { get; set; }

    [DynamoDBProperty] public int Price { get; set; }

    [DynamoDBProperty] public int Amount { get; set; }

    [DynamoDBProperty("GSI1PK")] public string Gsi1SymbolSide { get; set; } = null!;

    [DynamoDBProperty("GSI1SK")] public string Gsi1Price { get; set; } = null!;
}
