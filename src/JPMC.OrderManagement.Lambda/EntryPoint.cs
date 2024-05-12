using Amazon.Lambda.APIGatewayEvents;
using JPMC.OrderManagement.Lambda;
using JPMC.OrderManagement.LambdaHandler;

await EntryPoint.Run<AppSettings, AddOrderHandler, APIGatewayProxyRequest, APIGatewayProxyResponse>(
    configureServices: servicesCollection =>
    {
    });