namespace JPMC.OrderManagement.API.Models;

public class Order
{
    public required int Id { get; set; }

    public required string Symbol { get; set; }

    public required OrderSide Side { get; set; }

    public required int Amount { get; set; }

    public required int Price { get; set; }
}

public enum OrderSide
{
    Buy,
    Sell
}
