﻿using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using AWS.Logger;
using JPMC.OrderManagement.DataLoader.Service.Options;
using JPMC.OrderManagement.DataLoader.Service.Services;
using JPMC.OrderManagement.DataLoader.Service.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JPMC.OrderManagement.DataLoader.Service;

internal class Startup(IConfiguration configuration)
{
    private const string DataLoaderServiceOptionsConfigPath = "Service";
    private const string DataLoaderJobOptionsConfigPath = "Service:Job";

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSingleton<EntryPoint>()
            .AddSingleton(configuration)
            .AddSingleton<IDataLoaderService, DataLoaderService>()
            .AddAWSService<IAmazonS3>()
            .AddAWSService<IAmazonDynamoDB>()
            .AddSingleton(provider =>
            {
                var serviceOptions = provider.GetRequiredService<IOptions<ServiceOptions>>().Value;
                return new DynamoDBOperationConfig
                {
                    TableNamePrefix = $"{serviceOptions.Environment}.",
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
            .AddSingleton<IFileSystem, FileSystemService>()
            .AddLogging(loggingBuilder =>
            {
                var cloudWatchLogsEnable = configuration.GetValue<bool>("CloudWatchLogs:Enable");

                loggingBuilder.AddConsole();

                if (cloudWatchLogsEnable)
                {
                    var environment = configuration.GetValue<string>($"{DataLoaderServiceOptionsConfigPath}:{nameof(ServiceOptions.Environment)}") ?? "NA";
                    var jobId = Environment.GetEnvironmentVariable("AWS_BATCH_JOB_ID") ?? "NA";
                    var attemptNumber = Environment.GetEnvironmentVariable("AWS_BATCH_JOB_ATTEMPT") ?? "NA";

                    loggingBuilder.AddAWSProvider(new AWSLoggerConfig
                    {
                        DisableLogGroupCreation = true,
                        LibraryLogErrors = false,
                        LogGroup = configuration.GetValue<string>("CloudWatchLogs:LogGroup"),
                        LogStreamNameSuffix = $"{jobId} - {attemptNumber} - {environment}"
                    });
                }
            });

        services
            .AddOptions<DataLoaderJobOptions>()
            .ValidateOnStart()
            .Validate(validateOptions => !string.IsNullOrWhiteSpace(validateOptions.BucketName), $"Invalid value for {DataLoaderJobOptionsConfigPath}:{nameof(DataLoaderJobOptions.BucketName)}.")
            .Validate(validateOptions => !string.IsNullOrWhiteSpace(validateOptions.ObjectKey), $"Invalid value for {DataLoaderJobOptionsConfigPath}:{nameof(DataLoaderJobOptions.ObjectKey)}.")
            .BindConfiguration(DataLoaderJobOptionsConfigPath);

        services
            .AddOptions<ServiceOptions>()
            .BindConfiguration(DataLoaderServiceOptionsConfigPath);
    }
}