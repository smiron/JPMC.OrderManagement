using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Logger;
using JPMC.OrderManagement.API.Services;
using JPMC.OrderManagement.API.Services.Interfaces;
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

        if (fulfilledAmount < trade.Amount)
        {
            // TODO: consider what is the correct solution for returning in case we could not satisfy the trade
            return Results.BadRequest(
                "The order book doesn't have enough orders to satisfy the required trade amount. Please retry at a later time.");
        }

        var tradesDocumentTransactWrite = dynamoDbContext.GetTargetTable<DataModels.Trade>().CreateTransactWrite();
        var ordersDocumentTransactWrite = dynamoDbContext.GetTargetTable<DataModels.Order>().CreateTransactWrite();

        var tradeId = Guid.NewGuid().ToString("D");
        tradesDocumentTransactWrite.AddDocumentToPut(dynamoDbContext.ToDocument(new DataModels.Trade
        {
            Pk = $"TRADE#{tradeId}",
            Sk = $"TRADE#{tradeId}",
            Amount = trade.Amount,
            EntityType = "TRADE",
            Id = tradeId,
            Side = trade.Side,
            Symbol = trade.Symbol
        }));

        foreach (var order in ordersToUpdate)
        {
            if (order.Amount > 0)
            {
                var updateConfig = new TransactWriteItemOperationConfig
                {
                    ConditionalExpression = new Expression
                    {
                        ExpressionStatement = "ETag = :currentETag",
                        ExpressionAttributeValues =
                        {
                            [":currentETag"] = new Primitive(order.ETag)
                        }
                    }
                };

                ordersDocumentTransactWrite.AddDocumentToUpdate(
                    dynamoDbContext.ToDocument(order),
                    updateConfig);
            }
            else
            {
                var deleteConfig = new TransactWriteItemOperationConfig
                {
                    ConditionalExpression = new Expression
                    {
                        ExpressionStatement = "ETag = :currentETag",
                        ExpressionAttributeValues =
                        {
                            [":currentETag"] = new Primitive(order.ETag)
                        }
                    }
                };

                ordersDocumentTransactWrite.AddItemToDelete(
                    dynamoDbContext.ToDocument(order),
                    deleteConfig);
            }
        }

        try
        {
            await tradesDocumentTransactWrite.Combine(ordersDocumentTransactWrite).ExecuteAsync();
            return Results.Created();
        }
        catch (TransactionCanceledException)
        {
            // TODO: determine how we should reach in this case. The customer needs to retry.
            return Results.BadRequest("The orders due to be used in this trade have been updated. Please retry.");
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
                Timestamp = DateTime.UtcNow,
                Successful = false,
                Reason = "The order book doesn't have enough orders to satisfy the required trade amount. Please retry at a later time."
            })
            : Results.Ok(new ApiModels.TradePrice
            {
                Timestamp = DateTime.UtcNow,
                Successful = true,
                Price = calculatedPrice
            });
    });

app.Run();
