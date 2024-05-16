using Amazon.DynamoDBv2.DataModel;

namespace JPMC.OrderManagement.Common.DataModels;

public abstract class EntityBase(string entityType)
{
    [DynamoDBHashKey(DynamoDbAttributes.Pk)]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey(DynamoDbAttributes.Sk)]
    public string Sk { get; set; } = null!;

    [DynamoDBProperty(DynamoDbAttributes.EntityType)]
    public string EntityType { get; set; } = entityType;

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