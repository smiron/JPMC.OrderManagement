namespace JPMC.OrderManagement.API.ApiModels;

internal abstract class TradeActionResult
{
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// This field is set to `true` if the API action has been successfully completed.
    /// </summary>
    public bool Successful { get; set; }

    public string? Reason { get; set; }
}

internal class TradePriceCalculationResult : TradeActionResult
{
    public int? Price { get; set; }
}

internal class TradePlacementResult : TradeActionResult
{
}