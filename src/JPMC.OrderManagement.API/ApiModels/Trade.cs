namespace JPMC.OrderManagement.API.ApiModels;

public class Trade
{
    public string Symbol { get; set; } = null!;

    public Side Side { get; set; }

    public int Amount { get; set; }
}
