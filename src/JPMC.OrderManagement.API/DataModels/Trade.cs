using Amazon.DynamoDBv2.DataModel;
using JPMC.OrderManagement.API.ApiModels;

namespace JPMC.OrderManagement.API.DataModels;

[DynamoDBTable("jpmc.ordermanagement")]
public class Trade : EntityBase
{
    [DynamoDBProperty] public string Symbol { get; set; } = null!;

    [DynamoDBProperty] public Side Side { get; set; }

    [DynamoDBProperty] public int Amount { get; set; }
}