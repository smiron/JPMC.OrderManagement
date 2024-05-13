using System.Diagnostics;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.Utils;
using Microsoft.AspNetCore.Mvc;

const string swaggerDocumentTitle = "OrderManagementAPI";
const string swaggerDocumentVersion = "v1";

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix)
    .AddJsonFile("appsettings.json", false, true)
    .Build();

var awsOptions = configuration.GetAWSOptions();
awsOptions.Region = RegionEndpoint.EUWest2;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddAWSService<IAmazonDynamoDB>()
    .AddDefaultAWSOptions(awsOptions)
    .AddSingleton<IDynamoDBContext, DynamoDBContext>(provider =>
    {
        var dynamoDbClient = provider.GetRequiredService<IAmazonDynamoDB>();
        var webHostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();

        return new DynamoDBContext(
            dynamoDbClient,
            new DynamoDBOperationConfig
            {
                TableNamePrefix = $"{webHostEnvironment.EnvironmentName}.",
                SkipVersionCheck = true,
                DisableFetchingTableMetadata = true
            });
    })
    .AddEndpointsApiExplorer()
    .AddOpenApiDocument(config =>
    {
        config.DocumentName = swaggerDocumentTitle;
        config.Title = $"{swaggerDocumentTitle} {swaggerDocumentVersion}";
        config.Version = swaggerDocumentVersion;
    })
    .AddLogging()
    .AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = swaggerDocumentTitle;
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

app.MapGet(
    "/orders/{id:int}", async (int id, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var order = await dynamoDbContext.LoadAsync<Order>($"order#{id}", $"order#{id}");
        stopwatch.Stop();
        logger.LogInformation("DynamoDB time: {DynamoDBTime}", stopwatch.ElapsedMilliseconds);

        return order;
    });

app.MapHealthChecks("/health");

app.Run();

[DynamoDBTable("jpmc.ordermanagement")]
public record Order
{
    [DynamoDBHashKey] public string PK { get; set; } = null!;

    [DynamoDBRangeKey] public string SK { get; set; } = null!;

    [DynamoDBVersion] public int? Version { get; set; }

    [DynamoDBProperty] public string EntityType { get; set; } = null!;

    [DynamoDBProperty] public int ID { get; set; }

    [DynamoDBProperty] public string Symbol { get; set; } = null!;

    [DynamoDBProperty] public Side Side { get; set; }

    [DynamoDBProperty] public int Amount { get; set; }

    [DynamoDBProperty] public int Price { get; set; }

    [DynamoDBProperty] public DateTime CreateTimestamp { get; set; } = DateTime.UtcNow;

    [DynamoDBProperty] public DateTime? UpdateTimestamp { get; set; }
}

public enum Side
{
    Buy,
    Sell
}