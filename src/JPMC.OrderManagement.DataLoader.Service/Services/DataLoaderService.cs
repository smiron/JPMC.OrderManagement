using Amazon.S3;
using Amazon.S3.Model;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;
using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.Common.DataModels;
using JPMC.OrderManagement.DataLoader.Service.Services.Interfaces;
using JPMC.OrderManagement.DataLoader.Service.Options;

namespace JPMC.OrderManagement.DataLoader.Service.Services;

internal class DataLoaderService(
    IOptions<ServiceOptions> serviceOptions,
    IOptions<DataLoaderJobOptions> jobOptions,
    IAmazonS3 amazonS3Client,
    IDynamoDBContext dynamoDbContext,
    DynamoDBOperationConfig dynamoDbOperationConfig,
    ILogger<DataLoaderService> logger) : IDataLoaderService
{
    public async Task ExecuteAsync()
    {
        await DownloadData();

        await ImportData();
    }

    private async Task ImportData()
    {
        using var writer = new StreamReader(serviceOptions.Value.DownloadToFile);
        using var csvReader = new CsvReader(writer, CultureInfo.InvariantCulture);

        var orderLines = csvReader.GetRecordsAsync<OrderLine>();

        var batchWrite = dynamoDbContext.CreateBatchWrite<Order>(dynamoDbOperationConfig);

        // TODO: there might be a significant amount of data in the CSV file. We need to limit the maximum number of records we add to a batch
        await foreach (var orderLine in orderLines)
        {
            var order = new Order(orderLine.OrderId)
            {
                Side = orderLine.Side,
                Symbol = orderLine.Symbol,
                Amount = orderLine.Amount,
                Price = orderLine.Price
            };

            batchWrite.AddPutItem(order);
        }

        try
        {
            await batchWrite.ExecuteAsync();
            logger.LogInformation("Data load complete.");
        }
        catch (Exception ex)
        {
            logger.LogError("Data load encountered an error: {ErrorMessage}. Exception: {Exception}", ex.Message, ex);
        }
    }

    private async Task DownloadData()
    {
        logger.LogInformation("Loading data from bucket {BucketName} and object key {ObjectKey}",
            jobOptions.Value.BucketName, jobOptions.Value.ObjectKey);

        using var getObjectResponse = await amazonS3Client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = jobOptions.Value.BucketName,
                Key = jobOptions.Value.ObjectKey
            });

        await using Stream responseStream = getObjectResponse.ResponseStream;

        await using var fileStream = File.Create(serviceOptions.Value.DownloadToFile);

        await responseStream.CopyToAsync(fileStream);

        logger.LogInformation("The data has been downloaded locally for reliable ingestion.");
    }
}

[DebuggerDisplay("OrderId={OrderId}, Symbol={Symbol}, Side={Side}, Amount={Amount}, Price={Price}")]
public class OrderLine
{
    [Name("orderId")]
    [Index(0)]
    public required int OrderId { get; set; }

    [Name("symbol")]
    [Index(1)]
    public required string Symbol { get; set; }

    [Name("side")]
    [Index(2)]
    public required Side Side { get; set; }

    [Name("amount")]
    [Index(3)]
    public required int Amount { get; set; }

    [Name("price")]
    [Index(4)]
    public required int Price { get; set; }
}