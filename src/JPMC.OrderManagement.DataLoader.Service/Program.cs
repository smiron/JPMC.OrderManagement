using JPMC.OrderManagement.Common;
using JPMC.OrderManagement.DataLoader.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix)
    .AddCommandLine(args)
    .Build();

var startup = new Startup(configuration);
startup.ConfigureServices(services);

await using var serviceProvider = services.BuildServiceProvider();

var app = serviceProvider.GetRequiredService<EntryPoint>();
await app.RunAsync();