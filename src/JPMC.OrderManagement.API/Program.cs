using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
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

var createItemOperationConfig = new PutItemOperationConfig
{
    ConditionalExpression = new Expression
    {
        ExpressionStatement = "attribute_not_exists(ID)"
    }
};

var updateItemOperationConfig = new UpdateItemOperationConfig
{
    ConditionalExpression = new Expression
    {
        ExpressionStatement = "attribute_exists(ID)"
    }
};

var deleteItemOperationConfig = new DeleteItemOperationConfig
{
    ConditionalExpression = new Expression
    {
        ExpressionStatement = "attribute_exists(ID)"
    }
};

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
        var order = await dynamoDbContext.LoadAsync<DataModels.Order>($"ORDER#{id}", $"ORDER#{id}");

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
    async (int id, [FromBody] ApiModels.CreateUpdateOrder order, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        try
        {
            var createOrderDocument = dynamoDbContext.ToDocument(new DataModels.Order
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
                Gsi1Price = $"{order.Price:0000000}"
            });

            await dynamoDbContext.GetTargetTable<DataModels.Order>().PutItemAsync(
                createOrderDocument,
                createItemOperationConfig);

            return Results.Created();
        }
        catch (ConditionalCheckFailedException)
        {
            return Results.Conflict("An Order with the same ID already exists.");
        }
    });

// update orders
app.MapPut(
    "/orders/{id:int}",
    async (int id, [FromBody] ApiModels.CreateUpdateOrder order, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] IAmazonDynamoDB amazonDynamoDbClient, [FromServices] ILogger<Program> logger) =>
    {
        try
        {
            var updatedOrderDocument = dynamoDbContext.ToDocument(new DataModels.Order
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
                Gsi1Price = $"{order.Price:0000000}"
            });

            await dynamoDbContext.GetTargetTable<DataModels.Order>().UpdateItemAsync(
                updatedOrderDocument,
                updateItemOperationConfig);

            return Results.Ok();
        }
        catch(ConditionalCheckFailedException)
        {
            return Results.NotFound("Order does not exist.");
        }
    });

// delete orders
app.MapDelete(
    "/orders/{id:int}",
    async (int id, [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        try
        {
            var deleteOrderDocument = dynamoDbContext.ToDocument(new DataModels.Order
            {
                Pk = $"ORDER#{id}",
                Sk = $"ORDER#{id}"
            });

            await dynamoDbContext.GetTargetTable<DataModels.Order>().DeleteItemAsync(
                deleteOrderDocument,
                deleteItemOperationConfig);

            return Results.Ok();
        }
        catch (ConditionalCheckFailedException)
        {
            return Results.NotFound("Order does not exist.");
        }
    });

app.MapPost("/trade",
    async ([FromBody] ApiModels.Trade trade,
        [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        // look for orders that can take the other side of the trade
        var orderBookSide = trade.Side == ApiModels.Side.Buy
            ? ApiModels.Side.Sell
            : ApiModels.Side.Buy;

        var orderQuery =
            dynamoDbContext.QueryAsync<DataModels.Order>(
                $"{trade.Symbol}#{orderBookSide}",
                new DynamoDBOperationConfig
                {
                    // The best Buy price is the one of the order with the smallest price in the book -> traverse the index forward
                    // The best Buy price is the one of the order with the smallest price in the book -> traverse the index backward
                    BackwardQuery = trade.Side != ApiModels.Side.Buy,
                    IndexName = "GSI1"
                });

        var ordersToUpdate = new List<DataModels.Order>();
        int fulfilledAmount = 0;

        // Iterate as long as there are more orders to go through, and we've not fulfilled the trade amount.
        while (!orderQuery.IsDone && fulfilledAmount < trade.Amount)
        {
            var ordersPage = await orderQuery.GetNextSetAsync();

            foreach (var order in ordersPage)
            {
                int orderFulfilledAmount = trade.Amount - fulfilledAmount > order.Amount ? order.Amount : trade.Amount - fulfilledAmount;
                fulfilledAmount += orderFulfilledAmount;
                order.Amount -= orderFulfilledAmount;
                ordersToUpdate.Add(order);

                if (fulfilledAmount >= trade.Amount)
                {
                    // End the order processing loop early if we've already fulfilled the trade amount
                    break;
                }
            }
        }
    });

app.MapPost("/trade/price",
    async ([FromBody] ApiModels.Trade trade,
        [FromServices] IDynamoDBContext dynamoDbContext, [FromServices] ILogger<Program> logger) =>
    {
        var orderQuery =
            dynamoDbContext.QueryAsync<DataModels.Order>(
                $"{trade.Symbol}#{trade.Side}",
                new DynamoDBOperationConfig
                {
                    // The best Buy price is the one of the order with the smallest price in the book -> traverse the index forward
                    // The best Sell price is the one of the order with the highest price in the book -> traverse the index backward
                    BackwardQuery = trade.Side == ApiModels.Side.Sell,
                    IndexName = "GSI1"
                });

        int fulfilledAmount = 0;
        int calculatedPrice = 0;

        // Iterate as long as there are more orders to go through, and we've not fulfilled the trade amount.
        while (!orderQuery.IsDone && fulfilledAmount < trade.Amount)
        {
            var ordersPage = await orderQuery.GetNextSetAsync();

            foreach (var order in ordersPage)
            {
                int orderFulfilledAmount = trade.Amount - fulfilledAmount > order.Amount ? order.Amount : trade.Amount - fulfilledAmount;
                calculatedPrice += orderFulfilledAmount * order.Price;
                fulfilledAmount += orderFulfilledAmount;

                if (fulfilledAmount >= trade.Amount)
                {
                    // End the order processing loop early if we've already fulfilled the trade amount
                    break;
                }
            }
        }

        return fulfilledAmount < trade.Amount
            ? Results.Ok(new ApiModels.TradePrice
            {
                Successful = false,
                Timestamp = DateTime.UtcNow
            })
            : Results.Ok(new ApiModels.TradePrice
            {
                Successful = true,
                Price = calculatedPrice,
                Timestamp = DateTime.UtcNow
            });
    });

app.Run();
