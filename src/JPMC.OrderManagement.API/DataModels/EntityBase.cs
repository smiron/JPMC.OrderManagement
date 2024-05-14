using Amazon.DynamoDBv2.DataModel;

namespace JPMC.OrderManagement.API.DataModels;

public class EntityBase
{
    [DynamoDBHashKey(Attributes.Pk)] public required string Pk { get; set; }

    [DynamoDBRangeKey(Attributes.Sk)] public required string Sk { get; set; }

    [DynamoDBProperty(Attributes.EntityType)] public string EntityType { get; set; } = null!;

    [DynamoDBProperty(Attributes.Id)] public string Id { get; set; } = null!;

    [DynamoDBProperty(Attributes.ETag)] public string ETag { get; set; } = Guid.NewGuid().ToString("D");

    [DynamoDBProperty(Attributes.Gsi1Pk)] public string? Gsi1Pk { get; set; }

    [DynamoDBProperty(Attributes.Gsi1Sk)] public string? Gsi1Sk { get; set; }
}