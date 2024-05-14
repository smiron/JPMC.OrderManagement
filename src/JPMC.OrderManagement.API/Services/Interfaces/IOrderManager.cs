using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.Services.Interfaces;

/// <summary>
/// This interface reflects the OrderManager interface mentioned in the "Coding Challenge - Order Management" document.
/// </summary>
public interface IOrderManager
{
    Task<Order?> GetOrder(int orderId);

    Task AddOrder(int orderId, string symbol, Side side, int amount, int price);

    Task ModifyOrder(int orderId, int amount, int price);
}