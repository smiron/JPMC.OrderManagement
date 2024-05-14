namespace JPMC.OrderManagement.API.ApiModels;

public class Trade
{
    public string Symbol { get; set; } = null!;

    public Side Side { get; set; }

    public int Amount { get; set; }
}

public abstract class TradeActionResult
{
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// This field is set to `true` if the API action has been successfully completed.
    /// </summary>
    public bool Successful { get; set; }

    public string? Reason { get; set; }
}

public class TradePriceCalculationResult : TradeActionResult
{
    public int? Price { get; set; }
}

public class TradePlacementResult : TradeActionResult
{
}