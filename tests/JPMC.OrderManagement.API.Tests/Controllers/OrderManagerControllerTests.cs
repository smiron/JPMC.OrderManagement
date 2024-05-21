using JPMC.OrderManagement.API.Controllers;
using JPMC.OrderManagement.API.Services.Interfaces;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using JPMC.OrderManagement.Common.DataModels;
using Microsoft.Extensions.Logging;

namespace JPMC.OrderManagement.API.Tests.Controllers;

public class OrderManagerControllerTests
{
    private readonly IOrderManagerService _orderManagerService;
    private readonly IDateTimeService _dateTimeService;
    private readonly ILogger<OrderManagerController> _logger;

    private readonly OrderManagerController _subject;

    public OrderManagerControllerTests()
    {
        _orderManagerService = A.Fake<IOrderManagerService>();
        _dateTimeService = A.Fake<IDateTimeService>();
        _logger = A.Fake<ILogger<OrderManagerController>>();

        _subject = new OrderManagerController(_orderManagerService, _dateTimeService, _logger);
    }

    [Fact]
    public async Task GivenOrderData_WhenICallAddOrder_ThenIGetASuccessfulStatusCode()
    {
        // Arrange
        int orderId = 1;
        string symbol = "JPM";
        Side side = Side.Buy;
        int amount = 20;
        int price = 21;

        // Act
        var result = await _subject.AddOrder(orderId, symbol, side, amount, price);

        // Assert
        A.CallTo(() => _orderManagerService.AddOrder(orderId, symbol, side, amount, price))
            .MustHaveHappenedOnceExactly();
        Assert.NotNull(result);
        Assert.Equivalent(Results.Created(), result);
    }
}