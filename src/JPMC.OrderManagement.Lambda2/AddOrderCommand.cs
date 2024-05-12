namespace JPMC.OrderManagement.Lambda2;

public class AddOrderCommand
{
    public required int OrderId { get; set; }
    public required string Symbol { get; set; }
    public required Side Side { get; set; }
    public required int Amount { get; set; }
    public required int Price { get; set; }
}

public enum Side
{
    Buy,
    Sell
}
