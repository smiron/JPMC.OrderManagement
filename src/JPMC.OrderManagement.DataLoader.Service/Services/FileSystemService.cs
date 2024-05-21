using JPMC.OrderManagement.DataLoader.Service.Services.Interfaces;

namespace JPMC.OrderManagement.DataLoader.Service.Services;

internal class FileSystemService : IFileSystem
{
    public FileStream Open(string filepath)
    {
        return File.Create(filepath);
    }
}