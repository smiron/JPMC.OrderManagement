namespace JPMC.OrderManagement.DataLoader.Service.Options;

internal class ServiceOptions
{
    public string DownloadToFile { get; set; } = "./data.csv";

    public string Environment { get; set; } = null!;

    public string DynamoDbTableName { get; set; } = null!;
}