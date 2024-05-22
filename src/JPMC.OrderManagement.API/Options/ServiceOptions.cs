namespace JPMC.OrderManagement.API.Options;

internal class ServiceOptions
{
    public string BatchLoadingS3Bucket { get; set; } = null!;

    public string BatchLoadingS3ObjectPrefix { get; set; } = null!;

    public string DynamoDbTableName { get; set; } = null!;

    public bool HttpLogging { get; set; }
}