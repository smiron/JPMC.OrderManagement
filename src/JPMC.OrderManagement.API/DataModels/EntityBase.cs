using Amazon.DynamoDBv2.DataModel;

namespace JPMC.OrderManagement.API.DataModels;

public class EntityBase
{
    [DynamoDBHashKey(DynamoDbAttributes.Pk)]
    public required string Pk { get; set; }

    [DynamoDBRangeKey(DynamoDbAttributes.Sk)]
    public required string Sk { get; set; }

    [DynamoDBProperty(DynamoDbAttributes.EntityType)]
    public string EntityType { get; set; } = null!;

    [DynamoDBProperty(DynamoDbAttributes.Id)]
    public string Id { get; set; } = null!;

    [DynamoDBProperty(DynamoDbAttributes.ETag)]
    public string ETag { get; set; } = Guid.NewGuid().ToString("D");

    [DynamoDBProperty(DynamoDbAttributes.Gsi1Pk)]
    [DynamoDBGlobalSecondaryIndexHashKey("GSI1")]
    public string? Gsi1Pk { get; set; }

    [DynamoDBProperty(DynamoDbAttributes.Gsi1Sk)]
    [DynamoDBGlobalSecondaryIndexRangeKey("GSI1")]
    public string? Gsi1Sk { get; set; }
}