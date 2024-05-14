using Amazon.DynamoDBv2.DataModel;

namespace JPMC.OrderManagement.API.DataModels;

public class EntityBase
{
    [DynamoDBHashKey("PK")] public required string Pk { get; set; }

    [DynamoDBRangeKey("SK")] public required string Sk { get; set; }

    [DynamoDBProperty] public string EntityType { get; set; } = null!;

    [DynamoDBProperty("ID")] public int Id { get; set; }

    [DynamoDBProperty] public string ETag { get; set; } = Guid.NewGuid().ToString("D");

    [DynamoDBProperty("GSI1PK")] public string? Gsi1Pk { get; set; }

    [DynamoDBProperty("GSI1SK")] public string? Gsi1Sk { get; set; }
}