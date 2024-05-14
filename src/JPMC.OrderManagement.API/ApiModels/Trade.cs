namespace JPMC.OrderManagement.API.ApiModels;

public class Trade
{
    public string Symbol { get; set; } = null!;

    public Side Side { get; set; }

    public int Amount { get; set; }
}

public class TradePrice
{
    public DateTime Timestamp { get; set; }

    public int? Price { get; set; }

    /// <summary>
    /// This field is set to `true` if a trade price has been successfully calculated.
    /// This field is set to `false` if the order book doesn't have enough orders to satisfy the required trade amount. The customer needs to retry at a later time in this case.
    /// </summary>
    public bool Successful { get; set; }
}