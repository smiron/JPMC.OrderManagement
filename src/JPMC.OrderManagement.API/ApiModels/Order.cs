using JPMC.OrderManagement.Common.DataModels;

namespace JPMC.OrderManagement.API.ApiModels;

internal class Order : AddOrder
{
    public int Id { get; set; }
}

internal class AddOrder
{
    public required string Symbol { get; set; }

    public required Side Side { get; set; }

    public required int Amount { get; set; }

    public required int Price { get; set; }
}

internal class ModifyOrder
{
    public required int Amount { get; set; }

    public required int Price { get; set; }
}