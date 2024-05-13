using System.Diagnostics;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Logger;
using JPMC.OrderManagement.Utils;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;

const string swaggerDocumentTitle = "OrderManagementAPI";
const string swaggerDocumentVersion = "v1";

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix)
    .AddJsonFile("appsettings.json", false, true)
    .Build();

var awsOptions = configuration.GetAWSOptions();
awsOptions.Region = RegionEndpoint.EUWest2;

AWSXRayRecorder.InitializeInstance(configuration);
AWSSDKHandler.RegisterXRayForAllServices();

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
    .AddLogging(loggingBuilder => loggingBuilder
        .AddAWSProvider(new AWSLoggerConfig
        {
            DisableLogGroupCreation = true,
            Region = "eu-west-2",
            LogGroup = "/ecs/jpmc-order-management-api",
            LogStreamNamePrefix = "ecs"
        }))
    .AddHttpLogging(options =>
    {
        options.CombineLogs = true;
        options.LoggingFields = HttpLoggingFields.Duration
                                | HttpLoggingFields.RequestPath
                                | HttpLoggingFields.RequestMethod
                                | HttpLoggingFields.RequestProtocol
                                | HttpLoggingFields.RequestScheme
                                | HttpLoggingFields.ResponseStatusCode;
    })
    .AddHealthChecks();

var app = builder.Build();

app.UsePathBase(new PathString("/api"));

app.UseHttpLogging();

if (app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment("dev")
    || app.Environment.IsEnvironment("local"))
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