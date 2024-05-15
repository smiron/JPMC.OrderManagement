using System.Runtime.InteropServices.JavaScript;
using JPMC.OrderManagement.API.ApiModels;
using Order = JPMC.OrderManagement.API.DataModels.Order;

namespace JPMC.OrderManagement.API.Tests.DataModels;

public class OrderTests
{
    [Fact]
    internal void GivenAnOrderWithASpecificId_WhenICheckTheHashKeyAndRangeKey_ThenTheValuesAreCorrectlySet()
    {
        // Arrange
        var order = new Order(5);

        // Act
        // nothing to do here

        // Assert
        Assert.Equal("ORDER#5", order.Pk);
        Assert.Equal("ORDER#5", order.Sk);
    }

    [Theory]
    [InlineData("JPM", Side.Buy, 10)]
    internal void GivenAnOrder_WhenISetValuesToSymbolSideOrPrice_ThenTheIndexValuesAreCorrectlySet(string symbol, Side side, int price)
    {
        // Arrange
        // ReSharper disable once UseObjectOrCollectionInitializer
        var order = new Order(5);

        // Act
        order.Symbol = symbol;
        order.Side = side;
        order.Price = price;

        // Assert
        Assert.Equal($"{symbol}#{side}", order.Gsi1Pk);
        Assert.Equal($"{price:0000000}", order.Gsi1Sk);
    }
}