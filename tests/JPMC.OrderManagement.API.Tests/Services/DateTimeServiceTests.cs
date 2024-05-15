using JPMC.OrderManagement.API.Services;

namespace JPMC.OrderManagement.API.Tests.Services;

public class DateTimeServiceTests
{
    private readonly DateTimeService _subject = new();

    [Fact]
    public void GivenADateTimeService_WhenICallUtcNow_ThenIGetTheUtcNowDateTime()
    {
        // Arrange
        // nothing to do here

        // Act
        var actualResult = _subject.UtcNow;

        // Assert
        var expectedResult = DateTime.UtcNow;
        Assert.InRange(expectedResult.Subtract(actualResult).TotalMilliseconds, 0, 20);
    }
}