using NSwag.AspNetCore;

using JPMC.OrderManagement.API.Models;

const string swaggerDocumentTitle = "OrderManagementAPI";
const string swaggerDocumentVersion = "v1";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = swaggerDocumentTitle;
    config.Title = $"{swaggerDocumentTitle} {swaggerDocumentVersion}";
    config.Version = swaggerDocumentVersion;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
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

app.MapGet("/orders", async () =>
    new []
    {
        new Order
        {
            Id = 1,
            Symbol = "JPM",
            Side = OrderSide.Buy,
            Amount = 20,
            Price = 20
        },
        new Order
        {
            Id = 1,
            Symbol = "GOOG",
            Side = OrderSide.Sell,
            Amount = 12,
            Price = 25
        },
        new Order
        {
            Id = 1,
            Symbol = "AMZN",
            Side = OrderSide.Sell,
            Amount = 7,
            Price = 10
        },
        new Order
        {
            Id = 1,
            Symbol = "JPM",
            Side = OrderSide.Buy,
            Amount = 10,
            Price = 21
        }
    });

app.Run();
