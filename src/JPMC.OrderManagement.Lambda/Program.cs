using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

await LambdaBootstrapBuilder.Create<APIGatewayProxyRequest>(Handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();

async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
{
    DynamoDBContext dynamoDbContext;
    
    return new APIGatewayProxyResponse 
    {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonSerializer.Serialize(new
        {
            A = "A"
        })
    };
};