namespace JPMC.OrderManagement.DataLoader.Service.Services;

internal class DataLoaderJobOptions
{
    public string BucketName { get; set; } = null!;

    public string ObjectKey { get; set; } = null!;
}