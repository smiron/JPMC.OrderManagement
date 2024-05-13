using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Logger;
using JPMC.OrderManagement.Utils;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;

using ApiModels = JPMC.OrderManagement.API.ApiModels;
using DataModels = JPMC.OrderManagement.API.DataModels;

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
    async (int id, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        var order = await dynamoDbContext.LoadAsync<DataModels.Order>($"order#{id}", $"order#{id}");

        return order == null
            ? Results.NotFound()
            : Results.Ok(new ApiModels.Order
            {
                Id = order.Id,
                Symbol = order.Symbol,
                Side = order.Side,
                Amount = order.Amount,
                Price = order.Price
            });
    });

// create orders
app.MapPost(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.Order order, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        // TODO: create if not exists
        await dynamoDbContext.SaveAsync(
            new DataModels.Order
            {
                Id = id,
                Symbol = order.Symbol,
                Side = order.Side,
                Amount = order.Amount,
                Price = order.Price,
                EntityType = "ORDER",
                Pk = $"ORDER#{id}",
                Sk = $"ORDER#{id}",
                Gsi1SymbolSide = $"{order.Symbol}#{order.Side}",
                Gsi1Price = order.Price
            },
            new DynamoDBOperationConfig
            {
                
            });

        return Results.Created();
    });

// update orders
app.MapPut(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.Order order, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        await dynamoDbContext.SaveAsync(new DataModels.Order
        {
            Id = id,
            Symbol = order.Symbol,
            Side = order.Side,
            Amount = order.Amount,
            Price = order.Price,
            EntityType = "ORDER",
            Pk = $"ORDER#{id}",
            Sk = $"ORDER#{id}",
            Gsi1SymbolSide = $"{order.Symbol}#{order.Side}",
            Gsi1Price = order.Price
        });

        return Results.Ok();
    });

// delete orders
app.MapDelete(
    "/orders/{id:int}",
    async (int id, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        await dynamoDbContext.DeleteAsync<DataModels.Order>($"ORDER#{id}", $"ORDER#{id}");

        return Results.Ok();
    });

app.Run();
