using JPMC.OrderManagement.API.ApiModels;
using JPMC.OrderManagement.API.Controllers.Interfaces;
using JPMC.OrderManagement.API.Services;
using JPMC.OrderManagement.API.Services.Interfaces;

namespace JPMC.OrderManagement.API.Controllers;

internal class OrderManagerController(IOrderManager orderManager, IDateTimeService dateTimeService) : IOrderManagerController
{
    public async Task<IResult> GetOrder(int orderId)
    {
        var order = await orderManager.GetOrder(orderId);

        return order == null
            ? Results.NotFound()
            : Results.Ok(order);
    }

    public async Task<IResult> AddOrder(int orderId, string symbol, Side side, int amount, int price)
    {
        try
        {
            await orderManager.AddOrder(orderId, symbol, side, amount, price);
            return Results.Created();
        }
        catch (OrderManagerException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }

    public async Task<IResult> ModifyOrder(int orderId, int amount, int price)
    {
        try
        {
            await orderManager.ModifyOrder(orderId, amount, price);
            return Results.Ok();
        }
        catch (OrderManagerException)
        {
            return Results.NotFound("Order does not exist.");
        }
    }

    public async Task<IResult> RemoveOrder(int orderId)
    {
        try
        {
            await orderManager.RemoveOrder(orderId);
            return Results.Ok();
        }
        catch (OrderManagerException)
        {
            return Results.NotFound("Order does not exist.");
        }
    }

    public async Task<IResult> CalculatePrice(string symbol, Side side, int amount)
    {
        try
        {
            var tradePrice = await orderManager.CalculatePrice(symbol, side, amount);
            return Results.Ok(new TradePriceCalculationResult
            {
                Timestamp = dateTimeService.UtcNow,
                Successful = true,
                Price = tradePrice
            });
        }
        catch (OrderManagerException ex)
        {
            return Results.Ok(new TradePriceCalculationResult
            {
                Timestamp = dateTimeService.UtcNow,
                Successful = false,
                Reason = ex.Message
            });
        }
    }

    public async Task<IResult> PlaceTrade(string symbol, Side side, int amount)
    {
        try
        {
            await orderManager.PlaceTrade(symbol, side, amount);
            return Results.Ok(new TradePlacementResult
            {
                Timestamp = dateTimeService.UtcNow,
                Successful = true
            });
        }
        catch (OrderManagerException ex)
        {
            return Results.Ok(new TradePlacementResult
            {
                Timestamp = dateTimeService.UtcNow,
                Successful = false,
                Reason = ex.Message
            });
        }
    }
}