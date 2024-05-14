using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

// TODO: inject the table name dynamically
[DynamoDBTable("jpmc.ordermanagement")]
public class Order : EntityBase
{
    [DynamoDBProperty] public string Symbol { get; set; } = null!;

    [DynamoDBProperty] public Side Side { get; set; }

    [DynamoDBProperty] public int Price { get; set; }

    [DynamoDBProperty] public int Amount { get; set; }

    [DynamoDBIgnore]
    public string Gsi1SymbolSide
    {
        get => Gsi1Pk;
        set => Gsi1Pk = value;
    }

    [DynamoDBIgnore]
    public string Gsi1Price
    {
        get => Gsi1Sk;
        set => Gsi1Sk = value;
    }
}
