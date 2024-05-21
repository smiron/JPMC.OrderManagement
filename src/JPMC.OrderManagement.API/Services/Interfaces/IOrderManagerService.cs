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

    Task<OrderBatchLoad> BatchLoad();

    Task<int> CalculatePrice(string symbol, Side side, int amount);

    Task PlaceTrade(string symbol, Side side, int amount);
}

internal class OrderBatchLoad
{
    public string PreSignedUrl { get; set; } = null!;

    public DateTime CreationTimestampUtc { get; set; }

    public DateTime ExpirationTimestampUtc { get; set; }
}