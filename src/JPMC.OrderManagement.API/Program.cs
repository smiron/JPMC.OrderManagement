using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Logger;
using JPMC.OrderManagement.API.Controllers;
using JPMC.OrderManagement.API.Controllers.Interfaces;
using JPMC.OrderManagement.API.Options;
using JPMC.OrderManagement.API.Services;
using JPMC.OrderManagement.API.Services.Interfaces;
using JPMC.OrderManagement.Common;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ApiModels = JPMC.OrderManagement.API.ApiModels;

var swaggerDocumentTitle = $"{Constants.System}API";
var swaggerDocumentVersion = "v1";

const string serviceOptionsConfigPath = "Service";

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, false)
    .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix)
    .Build();

var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "local";
var xrayEnable = configuration.GetValue<bool>("XRay:Enable");
var cloudWatchLogsEnable = configuration.GetValue<bool>("CloudWatchLogs:Enable");
var cloudWatchLogGroup = configuration.GetValue<string>("CloudWatchLogs:LogGroup");

var httpLogging = configuration.GetValue<bool>($"{serviceOptionsConfigPath}:{nameof(ServiceOptions.HttpLogging)}");

var awsOptions = configuration.GetAWSOptions();

if (xrayEnable)
{
    AWSXRayRecorder.InitializeInstance(configuration);
    AWSSDKHandler.RegisterXRayForAllServices();    
}

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddAWSService<IAmazonDynamoDB>()
    .AddAWSService<IAmazonS3>()
    .AddDefaultAWSOptions(awsOptions)
    .AddSingleton(provider =>
    {
        var webHostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();
        var serviceOptions = provider.GetRequiredService<IOptions<ServiceOptions>>();

        return new DynamoDBOperationConfig
        {
            TableNamePrefix = $"{webHostEnvironment.EnvironmentName}.",
            OverrideTableName = serviceOptions.Value.DynamoDbTableName,
            SkipVersionCheck = true
        };
    })
    .AddSingleton<IDynamoDBContext, DynamoDBContext>(provider =>
    {
        var dynamoDbClient = provider.GetRequiredService<IAmazonDynamoDB>();
        var dynamoDbOperationConfig = provider.GetRequiredService<DynamoDBOperationConfig>();

        return new DynamoDBContext(
            dynamoDbClient,
            dynamoDbOperationConfig);
    })
    .AddSingleton<IOrderManagerService, OrderManagerService>()
    .AddSingleton<IOrderManagerController, OrderManagerController>()
    .AddSingleton<IDateTimeService, DateTimeService>()
    .AddEndpointsApiExplorer()
    .AddOpenApiDocument(config =>
    {
        config.DocumentName = swaggerDocumentTitle;
        config.Title = $"{swaggerDocumentTitle} {swaggerDocumentVersion}";
        config.Version = swaggerDocumentVersion;
    })
    .AddLogging(loggingBuilder =>
    {
        if (!cloudWatchLogsEnable) 
            return;

        loggingBuilder
            .AddAWSProvider(new AWSLoggerConfig
            {
                LogGroup = cloudWatchLogGroup,
                LibraryLogErrors = false,
                LogStreamNameSuffix = environmentName
            });
    })
    .AddHttpLogging(options =>
    {
        options.CombineLogs = true;
        options.LoggingFields = HttpLoggingFields.Duration
                                | HttpLoggingFields.RequestPath
                                | HttpLoggingFields.RequestMethod
                                | HttpLoggingFields.RequestProtocol
                                | HttpLoggingFields.RequestScheme
                                | HttpLoggingFields.ResponseStatusCode
                                | HttpLoggingFields.RequestQuery;
    })
    .AddHealthChecks();

builder.Services.AddOptions<ServiceOptions>().BindConfiguration(serviceOptionsConfigPath);

var app = builder.Build();

app.UsePathBase(new PathString("/api"));

if (httpLogging)
{
    app.UseHttpLogging();
}

app.MapHealthChecks("/health");

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

// Read orders
app.MapGet(
    "/orders/{id:int}",
    async (int id, 
        [FromServices] IOrderManagerController orderManager) => await orderManager.GetOrder(id));

// Add orders
app.MapPost(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.AddOrder order, 
        [FromServices] IOrderManagerController orderManager) => await orderManager.AddOrder(id, order.Symbol, order.Side, order.Amount, order.Price));

// Modify orders
app.MapPatch(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.ModifyOrder order, 
        [FromServices] IOrderManagerController orderManager) => await orderManager.ModifyOrder(id, order.Amount, order.Price));

// Remove orders
app.MapDelete(
    "/orders/{id:int}",
    async (int id, 
        [FromServices] IOrderManagerController orderManager) => await orderManager.RemoveOrder(id));

// Add orders
app.MapPost(
    "/orders/batch-load",
    async ([FromServices] IOrderManagerController orderManager) => await orderManager.BatchLoad());

// Trade placement
app.MapPost("/trade",
    async ([FromBody] ApiModels.Trade trade,
        [FromServices] IOrderManagerController orderManager) => await orderManager.PlaceTrade(trade.Symbol, trade.Side, trade.Amount));

// Trade price calculation
app.MapPost("/trade/price",
    async ([FromBody] ApiModels.Trade trade,
        [FromServices] IOrderManagerController orderManager) => await orderManager.CalculatePrice(trade.Symbol, trade.Side, trade.Amount));

app.Run();
