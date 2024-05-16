using JPMC.OrderManagement.DataLoader.Service.Services.Interfaces;

namespace JPMC.OrderManagement.DataLoader.Service;

internal class EntryPoint(IDataLoaderService dataLoaderService)
{
    public async Task RunAsync()
    {
        await dataLoaderService.ExecuteAsync();
    }
}