using JPMC.OrderManagement.API.ApiModels;

using Side = JPMC.OrderManagement.Common.DataModels.Side;

namespace JPMC.OrderManagement.API.Services.Interfaces;

/// <summary>
/// This interface reflects the OrderManager interface mentioned in the "Coding Challenge - Order Management" document.
/// </summary>
internal interface IOrderManagerService
{
    Task<Order?> GetOrder(int orderId);

    Task AddOrder(int orderId, string symbol, Side side, int amount, int price);

    Task ModifyOrder(int orderId, int amount, int price);

    Task RemoveOrder(int orderId);

    Task<int> CalculatePrice(string symbol, Side side, int amount);

    Task PlaceTrade(string symbol, Side side, int amount);
}