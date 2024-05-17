namespace JPMC.OrderManagement.DataLoader.Service.Services.Interfaces;

internal interface IFileSystem
{
    FileStream Open(string filepath);
}