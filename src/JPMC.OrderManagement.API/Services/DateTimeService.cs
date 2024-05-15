using JPMC.OrderManagement.API.Services.Interfaces;

namespace JPMC.OrderManagement.API.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}