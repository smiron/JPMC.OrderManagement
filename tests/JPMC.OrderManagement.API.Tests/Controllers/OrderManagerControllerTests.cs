using JPMC.OrderManagement.API.Controllers;
using JPMC.OrderManagement.API.Services.Interfaces;
using FakeItEasy;
using JPMC.OrderManagement.Common.Models;
using Microsoft.AspNetCore.Http;

namespace JPMC.OrderManagement.API.Tests.Controllers;

public class OrderManagerControllerTests
{
    private readonly IOrderManagerService _orderManagerService;
    private readonly IDateTimeService _dateTimeService;

    private readonly OrderManagerController _subject;

    public OrderManagerControllerTests()
    {
        _orderManagerService = A.Fake<IOrderManagerService>();
        _dateTimeService = A.Fake<IDateTimeService>();

        _subject = new OrderManagerController(_orderManagerService, _dateTimeService);
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