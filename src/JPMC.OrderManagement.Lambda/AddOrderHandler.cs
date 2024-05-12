using System.Net;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using AWS.Lambda.Powertools.Logging;
using JPMC.OrderManagement.LambdaHandler;
using Newtonsoft.Json;

namespace JPMC.OrderManagement.Lambda;

// ReSharper disable once ClassNeverInstantiated.Global
public class AddOrderHandler(IDynamoDBContext dynamoDbContext)
    : LambdaHandlerBase<APIGatewayProxyRequest, APIGatewayProxyResponse, AppSettings>
{
    private readonly IDynamoDBContext _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
    private DynamoDBOperationConfig? _dynamoDbOperationConfig;

    protected override void InitializeInternal()
    {
        base.InitializeInternal();

        Logger.LogInformation("Environment: {environment}", AppSettings!.Environment);

        _dynamoDbOperationConfig = new DynamoDBOperationConfig
        {
            TableNamePrefix = $"{AppSettings!.Environment}.",
            SkipVersionCheck = true,
            DisableFetchingTableMetadata = true
        };
    }

    protected override async Task<APIGatewayProxyResponse> HandleInternal(
        APIGatewayProxyRequest command,
        ILambdaContext context)
    {
        Logger.LogInformation("Retrieved command: {command}", command);

        try
        {
            var order = await _dynamoDbContext.LoadAsync<Order>("order#1", "order#1", _dynamoDbOperationConfig);

            Logger.LogInformation("Retrieved order: {order}", order);

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
    }

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
}