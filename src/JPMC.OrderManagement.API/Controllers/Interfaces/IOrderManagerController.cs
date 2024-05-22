using JPMC.OrderManagement.Common.DataModels;

namespace JPMC.OrderManagement.API.Controllers.Interfaces;

internal interface IOrderManagerController
{
    Task<IResult> GetOrder(int orderId);

    Task<IResult> AddOrder(int orderId, string symbol, Side side, int amount, int price);

    Task<IResult> ModifyOrder(int orderId, int amount, int price);

    Task<IResult> RemoveOrder(int orderId);

    Task<IResult> BatchLoad();

    Task<IResult> CalculatePrice(string symbol, Side side, int amount);

    Task<IResult> PlaceTrade(string symbol, Side side, int amount);
}