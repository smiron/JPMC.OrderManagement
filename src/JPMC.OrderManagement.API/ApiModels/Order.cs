namespace JPMC.OrderManagement.API.ApiModels;

public class Order : CreateUpdateOrder
{
    public int Id { get; set; }
}

public class CreateUpdateOrder
{
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
