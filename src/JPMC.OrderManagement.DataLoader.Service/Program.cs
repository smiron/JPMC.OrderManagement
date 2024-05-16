using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using JPMC.OrderManagement.Common;
using JPMC.OrderManagement.DataLoader.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

const string dataLoaderServiceOptionsConfigPath = "Service";
const string dataLoaderJobOptionsConfigPath = "Service:Job";

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args
});

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables(Constants.ComputeEnvironmentVariablesPrefix);

builder.Services
    .AddHostedService<DataLoaderBackgroundService>()
    .AddDefaultAWSOptions(provider => provider.GetRequiredService<IConfigurationRoot>().GetAWSOptions())
    .AddAWSService<IAmazonS3>()
    .AddAWSService<IAmazonDynamoDB>()
    .AddSingleton(provider =>
    {
        var serviceOptions = provider.GetRequiredService<IOptions<ServiceOptions>>().Value;
        return new DynamoDBOperationConfig
        {
            TableNamePrefix = $"{serviceOptions.EnvironmentName}.",
            OverrideTableName = serviceOptions.DynamoDbTableName,
            SkipVersionCheck = true
        };
    })
    .AddSingleton<IDynamoDBContext, DynamoDBContext>(provider =>
    {
        var dynamoDbClient = provider.GetRequiredService<IAmazonDynamoDB>();
        var dynamoDbOperationConfig = provider.GetRequiredService<DynamoDBOperationConfig>();

        return new DynamoDBContext(
            dynamoDbClient,
            dynamoDbOperationConfig);
    })
    .AddLogging(loggingBuilder => loggingBuilder.AddConsole())
    .Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    });

builder.Services
    .AddOptions<DataLoaderJobOptions>()
    .ValidateOnStart()
    .Validate(validateOptions => !string.IsNullOrWhiteSpace(validateOptions.BucketName), $"Invalid value for {dataLoaderJobOptionsConfigPath}:{nameof(DataLoaderJobOptions.BucketName)}.")
    .Validate(validateOptions => !string.IsNullOrWhiteSpace(validateOptions.ObjectKey), $"Invalid value for {dataLoaderJobOptionsConfigPath}:{nameof(DataLoaderJobOptions.ObjectKey)}.")
    .BindConfiguration(dataLoaderJobOptionsConfigPath);

builder.Services
    .AddOptions<ServiceOptions>()
    .BindConfiguration(dataLoaderServiceOptionsConfigPath);

var host = builder.Build();

await host.RunAsync();
