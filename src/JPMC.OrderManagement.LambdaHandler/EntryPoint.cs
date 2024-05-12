using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using JPMC.OrderManagement.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPMC.OrderManagement.LambdaHandler;

public static class EntryPoint
{
    public static async Task Run<TAppSettings, THandler, TCommand, TOutput>(
        Action<IConfigurationBuilder>? configureAppSettings = null,
        Action<IServiceCollection>? configureServices = null,
        string? appSettingsSectionKey = null)
        where THandler : class, IHandler<TCommand, TOutput, TAppSettings>
        where TAppSettings : new()
    {
        AWSSDKHandler.RegisterXRayForAllServices();

        var configurationBuilder = new ConfigurationBuilder()
            .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix);
            //.AddSystemsManager($"/{Constants.SolutionName}/{Environment.GetEnvironmentVariable(Constants.EnvironmentVariableName)}/", false);

        configureAppSettings?.Invoke(configurationBuilder);

        var configuration = configurationBuilder.Build();

        var appSettings =
            ((IConfiguration)(string.IsNullOrEmpty(appSettingsSectionKey)
                ? configuration
                : configuration.GetSection(appSettingsSectionKey)))
            .Get<TAppSettings>(options =>
            {
                options.ErrorOnUnknownConfiguration = false;
            })
            ?? new TAppSettings();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddAWSService<IAmazonDynamoDB>();
        serviceCollection.AddDefaultAWSOptions(configuration.GetAWSOptions());
        serviceCollection.AddSingleton<IDynamoDBContext, DynamoDBContext>();
        serviceCollection.AddTransient<IHandler<TCommand, TOutput, TAppSettings>, THandler>();

        configureServices?.Invoke(serviceCollection);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        IHandler<TCommand, TOutput, TAppSettings> handlerInstance = serviceProvider.GetRequiredService<IHandler<TCommand, TOutput, TAppSettings>>();
        handlerInstance.Initialize(appSettings);

        await LambdaBootstrapBuilder.Create(
                (Func<TCommand, ILambdaContext, Task<TOutput>>)handlerInstance.Handle,
                new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync();
    }
}