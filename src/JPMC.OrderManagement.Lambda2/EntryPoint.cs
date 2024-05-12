using Amazon.Lambda.APIGatewayEvents;
using JPMC.OrderManagement.Lambda2;
using JPMC.OrderManagement.LambdaHandler;

await EntryPoint.Run<AppSettings, AddOrderHandler, APIGatewayProxyRequest, APIGatewayProxyResponse>(
    configureServices: servicesCollection =>
    {
    });