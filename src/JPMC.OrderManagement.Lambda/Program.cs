using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var dynamoDbContext = new DynamoDBContext(new AmazonDynamoDBClient(), new DynamoDBContextConfig
{
    TableNamePrefix = "dev.",
    SkipVersionCheck = true,
    DisableFetchingTableMetadata = true
});
var orderTable = dynamoDbContext.GetTargetTable<Order>();

await LambdaBootstrapBuilder.Create<APIGatewayProxyRequest>(Handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
{
    try
    {

        context.Logger.LogInformation($"Received request: {JsonSerializer.Serialize(request)}");

        var order = await orderTable.GetItemAsync(new Primitive("order#1"), new Primitive("order#1"));

        context.Logger.LogInformation($"retrieved order: {JsonSerializer.Serialize(order)}");

        return order == null
            ? new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound
            }
            : new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonSerializer.Serialize(order)
            };
    }
    catch (Exception ex)
    {
        context.Logger.LogError($"Error message: {ex.Message}, Error: {ex}");
        throw;
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