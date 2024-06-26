﻿using JPMC.OrderManagement.API.ApiModels;
using JPMC.OrderManagement.API.Controllers.Interfaces;
using JPMC.OrderManagement.API.Services;
using JPMC.OrderManagement.API.Services.Interfaces;
using JPMC.OrderManagement.Common.DataModels;

namespace JPMC.OrderManagement.API.Controllers;

internal class OrderManagerController(IOrderManagerService orderManagerService, IDateTimeService dateTimeService, ILogger<OrderManagerController> logger) : IOrderManagerController
{
    public async Task<IResult> GetOrder(int orderId)
    {
        var order = await orderManagerService.GetOrder(orderId);

        return order == null
            ? Results.NotFound()
            : Results.Ok(order);
    }

    public async Task<IResult> AddOrder(int orderId, string symbol, Side side, int amount, int price)
    {
        try
        {
            await orderManagerService.AddOrder(orderId, symbol, side, amount, price);
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
            await orderManagerService.ModifyOrder(orderId, amount, price);
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
            await orderManagerService.RemoveOrder(orderId);
            return Results.Ok();
        }
        catch (OrderManagerException)
        {
            return Results.NotFound("Order does not exist.");
        }
    }

    public async Task<IResult> BatchLoad()
    {
        try
        {
            var batchLoadOrder = await orderManagerService.BatchLoad();
            return Results.Ok(batchLoadOrder);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Exception occurred while running the {nameof(BatchLoad)} operation.");
            return Results.StatusCode(500);
        }
    }

    public async Task<IResult> CalculatePrice(string symbol, Side side, int amount)
    {
        try
        {
            var tradePrice = await orderManagerService.CalculatePrice(symbol, side, amount);
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
            await orderManagerService.PlaceTrade(symbol, side, amount);
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