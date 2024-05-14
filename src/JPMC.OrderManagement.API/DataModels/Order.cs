using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

public class Order() : EntityBase(OrderEntityType)
{
    private string _symbol = string.Empty;
    private Side _side;
    private int _price;

    public const string OrderEntityType = "ORDER";

    public Order(int orderId) : this()
    {
        Id = orderId.ToString();
        Pk = $"{OrderEntityType}#{orderId}";
        Sk = $"{OrderEntityType}#{orderId}";
    }

    [DynamoDBProperty(DynamoDbAttributes.Symbol)]
    public string Symbol
    {
        get => _symbol;
        set
        {
            _symbol = value;
            UpdateIndexValues();
        }
    }

    [DynamoDBProperty(DynamoDbAttributes.Side)]
    public Side Side
    {
        get => _side;
        set
        {
            _side = value;
            UpdateIndexValues();
        }
    }

    [DynamoDBProperty(DynamoDbAttributes.Price)]
    public int Price
    {
        get => _price;
        set
        {
            _price = value;
            UpdateIndexValues();
        }
    }

    [DynamoDBProperty(DynamoDbAttributes.Amount)]
    public int Amount { get; set; }

    private void UpdateIndexValues()
    {
        Gsi1Pk = $"{_symbol}#{_side}";
        Gsi1Sk = $"{_price:0000000}";
    }
}