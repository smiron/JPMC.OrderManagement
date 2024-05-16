using JPMC.OrderManagement.Common.DataModels;
using Order = JPMC.OrderManagement.Common.DataModels.Order;

namespace JPMC.OrderManagement.Common.Tests.DataModels;

public class OrderTests
{
    [Fact]
    internal void GivenAnOrderWithASpecificId_WhenICheckTheHashKeyAndRangeKey_ThenTheValuesAreCorrectlySet()
    {
        // Arrange
        const int orderId = 5;
        var order = new Order(orderId);

        // Act
        // nothing to do here

        // Assert
        Assert.Equal(orderId.ToString(), order.Id);
        Assert.Equal($"ORDER#{orderId}", order.Pk);
        Assert.Equal($"ORDER#{orderId}", order.Sk);
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