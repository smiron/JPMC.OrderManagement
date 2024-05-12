using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

var dynamoDbContext = new DynamoDBContext(
    new AmazonDynamoDBClient(new AmazonDynamoDBConfig
    {
        RegionEndpoint = RegionEndpoint.EUWest2
    }),
    new DynamoDBContextConfig
    {
        TableNamePrefix = "dev.",
        SkipVersionCheck = true,
        DisableFetchingTableMetadata = true
    });

await LambdaBootstrapBuilder.Create<APIGatewayProxyRequest>(Handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
{
    try
    {
        var order = await dynamoDbContext.LoadAsync<Order>("order#1", "order#1");

        context.Logger.LogInformation($"Retrieved order: {JsonConvert.SerializeObject(order)}");

        if (order == null)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound
            };
        }

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = JsonConvert.SerializeObject(order),
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            }
        };
    }
    catch (Exception ex)
    {
        context.Logger.LogError($"Error message: {ex.Message}, Error: {ex}");
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Body = JsonConvert.SerializeObject(new { message = ex.Message })
        };
    }
};

[DynamoDBTable("jpmc.ordermanagement")]
public record Order
{
    [DynamoDBHashKey] public string PK { get; set; } = null!;

    [DynamoDBRangeKey] public string SK { get; set; } = null!;

    [DynamoDBVersion] public int? Version { get; set; }

    [DynamoDBProperty] public string EntityType { get; set; } = null!;

    [DynamoDBProperty] public int ID { get; set; }

    [DynamoDBProperty] public string Symbol { get; set; } = null!;

    [DynamoDBProperty] public OrderSide Side { get; set; }

    [DynamoDBProperty] public int Amount { get; set; }

    [DynamoDBProperty] public int Price { get; set; }

    [DynamoDBProperty] public DateTime CreateTimestamp { get; set; } = DateTime.UtcNow;

    [DynamoDBProperty] public DateTime? UpdateTimestamp { get; set; }
}

public enum OrderSide
{
    Buy,
    Sell
}