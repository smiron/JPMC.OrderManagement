namespace JPMC.OrderManagement.API.ApiModels;

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