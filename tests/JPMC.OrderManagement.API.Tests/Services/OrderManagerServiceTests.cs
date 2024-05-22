using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using FakeItEasy;
using JPMC.OrderManagement.API.Options;
using JPMC.OrderManagement.API.Services;
using JPMC.OrderManagement.Common.DataModels;
using Microsoft.Extensions.Options;

using Order = JPMC.OrderManagement.Common.DataModels.Order;

namespace JPMC.OrderManagement.API.Tests.Services;

public class OrderManagerServiceTests
{
    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonS3 _s3Client;
    private readonly IOptions<ServiceOptions> _serviceOptions;

    private readonly OrderManagerService _subject;

    public OrderManagerServiceTests()
    {
        _dynamoDbContext = A.Fake<IDynamoDBContext>();
        _s3Client = A.Fake<IAmazonS3>();
        _serviceOptions = A.Fake<IOptions<ServiceOptions>>();

        _subject = new OrderManagerService(_dynamoDbContext, _s3Client, _serviceOptions, new DynamoDBOperationConfig());
    }

    [Fact]
    public async Task GivenAnOrderId_WhenIGetOrder_ThenIGetTheOrderData()
    {
        // Arrange
        const int orderId = 1;
        var dynamoDbRecord = new Order(orderId)
        {
            Symbol = "JPM",
            Side = Side.Buy,
            Amount = 10,
            Price = 20
        };
        A.CallTo(() => _dynamoDbContext
            .LoadAsync<Order>("ORDER#1", "ORDER#1", A<DynamoDBOperationConfig>._, A<CancellationToken>._))
            .Returns(dynamoDbRecord);

        // Act
        var order = await _subject.GetOrder(orderId);

        // Assert
        A.CallTo(() =>
            _dynamoDbContext.LoadAsync<Order>("ORDER#1", "ORDER#1", A<DynamoDBOperationConfig>._,
                A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.NotNull(order);
        Assert.Equal(dynamoDbRecord.Id, order.Id.ToString());
        Assert.Equal(dynamoDbRecord.Symbol, order.Symbol);
        Assert.Equal(dynamoDbRecord.Side, order.Side);
        Assert.Equal(dynamoDbRecord.Amount, order.Amount);
        Assert.Equal(dynamoDbRecord.Price, order.Price);
    }

    [Fact]
    public async Task GivenAnInvalidOrderId_WhenIGetOrder_ThenIGetANullResult()
    {
        // Arrange
        const int orderId = 1;
        Order dynamoDbRecord = null!;
        A.CallTo(() => _dynamoDbContext
                .LoadAsync<Order>("ORDER#1", "ORDER#1", A<DynamoDBOperationConfig>._, A<CancellationToken>._))
            .Returns(dynamoDbRecord);

        // Act
        var order = await _subject.GetOrder(orderId);

        // Assert
        A.CallTo(() =>
            _dynamoDbContext.LoadAsync<Order>("ORDER#1", "ORDER#1", A<DynamoDBOperationConfig>._,
                A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.Null(order);
    }
}