using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

// TODO: inject the table name dynamically via appsettings keys
[DynamoDBTable("jpmc.ordermanagement")]
public class Order : EntityBase
{
    private string _symbol = string.Empty;
    private Side _side;
    private int _price;

    public Order()
    {
        EntityType = "ORDER";
    }

    [DynamoDBProperty(Attributes.Symbol)]
    public string Symbol
    {
        get => _symbol;
        set
        {
            _symbol = value;
            UpdateIndexValues();
        }
    }

    [DynamoDBProperty(Attributes.Side)]
    public Side Side
    {
        get => _side;
        set
        {
            _side = value;
            UpdateIndexValues();
        }
    }

    [DynamoDBProperty(Attributes.Price)]
    public int Price
    {
        get => _price;
        set
        {
            _price = value;
            UpdateIndexValues();
        }
    }

    [DynamoDBProperty(Attributes.Amount)]
    public int Amount
    {
        get;
        set;
    }

    private void UpdateIndexValues()
    {
        Gsi1Pk = $"{_symbol}#{_side}";
        Gsi1Sk = $"{_price:0000000}";
    }
}