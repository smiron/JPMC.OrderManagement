using JPMC.OrderManagement.Common.DataModels;

namespace JPMC.OrderManagement.API.Tests.DataModels;

public class TradeTests
{
    [Fact]
    internal void GivenATradeWithASpecificId_WhenICheckTheHashKeyAndRangeKey_ThenTheValuesAreCorrectlySet()
    {
        // Arrange
        const string tradeId = "MyID";
        var trade = new Trade(tradeId);

        // Act
        // nothing to do here

        // Assert
        Assert.Equal(tradeId, trade.Id);
        Assert.Equal($"TRADE#{tradeId}", trade.Pk);
        Assert.Equal($"TRADE#{tradeId}", trade.Sk);
    }
}