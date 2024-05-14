using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Logger;
using JPMC.OrderManagement.API.Services;
using JPMC.OrderManagement.API.Services.Interfaces;
using JPMC.OrderManagement.Utils;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;

using ApiModels = JPMC.OrderManagement.API.ApiModels;

var swaggerDocumentTitle = $"{Constants.System}API";
var swaggerDocumentVersion = "v1";

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, false)
    .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix)
    .Build();

var xrayEnable = configuration.GetValue<bool>("XRay:Enable");
var cloudWatchLogsEnable = configuration.GetValue<bool>("CloudWatchLogs:Enable");

var awsOptions = configuration.GetAWSOptions();
// TODO: remove region
awsOptions.Region = RegionEndpoint.EUWest2;

if (xrayEnable)
{
    AWSXRayRecorder.InitializeInstance(configuration);
    AWSSDKHandler.RegisterXRayForAllServices();    
}

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
                DisableFetchingTableMetadata = false
            });
    })
    .AddSingleton<IOrderManager, OrderManager>()
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
                Region = awsOptions.Region.SystemName,
                LogGroup = $"/{Constants.Owner}/{Constants.System}/api"
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

var app = builder.Build();

app.UsePathBase(new PathString("/api"));

app.UseHttpLogging();

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

// TODO: Add a service (CRUD Order) and inject it here.

// read orders
app.MapGet(
    "/orders/{id:int}",
    async (int id, [FromServices] IOrderManager orderManager) =>
    {
        var order = await orderManager.GetOrder(id);

        return order == null
            ? Results.NotFound()
            : Results.Ok(order);
    });

// Add orders
app.MapPost(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.AddOrder order, [FromServices] IOrderManager orderManager) =>
    {
        try
        {
            await orderManager.AddOrder(id, order.Symbol, order.Side, order.Amount, order.Price);
            return Results.Created();
        }
        catch (OrderManagerException ex)
        {
            return Results.Conflict(ex.Message);
        }
    });

// Modify orders
app.MapPut(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.ModifyOrder order, [FromServices] IOrderManager orderManager) =>
    {
        try
        {
            await orderManager.ModifyOrder(id, order.Amount, order.Price);
            return Results.Ok();
        }
        catch(OrderManagerException)
        {
            return Results.NotFound("Order does not exist.");
        }
    });

// Remove orders
app.MapDelete(
    "/orders/{id:int}",
    async (int id, [FromServices] IOrderManager orderManager) =>
    {
        try
        {
            await orderManager.RemoveOrder(id);
            return Results.Ok();
        }
        catch (OrderManagerException)
        {
            return Results.NotFound("Order does not exist.");
        }
    });

app.MapPost("/trade",
    async ([FromBody] ApiModels.Trade trade,
        [FromServices] IOrderManager orderManager) =>
    {
        try
        {
            await orderManager.PlaceTrade(trade.Symbol, trade.Side, trade.Amount);
            return Results.Ok(new ApiModels.TradePlacementResult
            {
                Timestamp = DateTime.UtcNow,
                Successful = true
            });
        }
        catch (OrderManagerException ex)
        {
            return Results.Ok(new ApiModels.TradePriceCalculationResult
            {
                Timestamp = DateTime.UtcNow,
                Successful = false,
                Reason = ex.Message
            });
        }
    });

app.MapPost("/trade/price",
    async ([FromBody] ApiModels.Trade trade,
        [FromServices] IOrderManager orderManager) =>
    {
        try
        {
            var tradePrice = await orderManager.CalculatePrice(trade.Symbol, trade.Side, trade.Amount);
            return Results.Ok(new ApiModels.TradePriceCalculationResult
            {
                Timestamp = DateTime.UtcNow,
                Successful = true,
                Price = tradePrice
            });
        }
        catch (OrderManagerException ex)
        {
            return Results.Ok(new ApiModels.TradePriceCalculationResult
            {
                Timestamp = DateTime.UtcNow,
                Successful = false,
                Reason = ex.Message
            });
        }
    });

app.Run();
