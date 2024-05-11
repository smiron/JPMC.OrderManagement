using Microsoft.Extensions.Configuration;

using Constructs;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;

using AmazonCDK = Amazon.CDK;

namespace JPMC.OrderManagement.Stack.Stacks;

internal class InfrastructureStack : AmazonCDK.Stack
{
    private readonly AppSettings _appSettings;

    internal InfrastructureStack(Construct scope)
        : base(scope, 
            $"{Constants.SolutionName}.{nameof(InfrastructureStack)}".Replace(".", "-"),
            new AmazonCDK.StackProps())
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .AddEnvironmentVariables(source => source.Prefix = "JPMC_")
            .Build();

        _appSettings = configuration
            .Get<AppSettings>(options =>
            {
                options.ErrorOnUnknownConfiguration = true;
            })!;

        //var fn = new Function(this, functionId, new FunctionProps
        //{
        //    FunctionName = $"{_appSettings.Environment}-{functionId}",
        //    Runtime = Runtime.DOTNET_6,
        //    Architecture = Architecture.X86_64,
        //    MemorySize = 256,
        //    Environment = new Dictionary<string, string>
        //            {
        //                {
        //                    Constants.EnvironmentVariableName, _appSettings.Environment
        //                },
        //                {
        //                    "POWERTOOLS_SERVICE_NAME", functionId
        //                },
        //                {
        //                    "POWERTOOLS_LOG_LEVEL", "Info"
        //                },
        //                {
        //                    "POWERTOOLS_LOGGER_LOG_EVENT", "true"
        //                },
        //                {
        //                    "POWERTOOLS_LOGGER_CASE", "CamelCase"
        //                },
        //                {
        //                    "POWERTOOLS_LOGGER_SAMPLE_RATE", "1"
        //                }
        //            },

        //    Timeout = AmazonCDK.Duration.Seconds(60),
        //    LogRetention = RetentionDays.ONE_WEEK,

        //    // functions using top level statements can just specify the main DLL name as the handler
        //    Handler = lambdaFunctionDirectory,
        //    Code = Code.FromDockerBuild(_appSettings.SourceDirectory, new DockerBuildAssetOptions
        //    {
        //        Platform = "linux/amd64",
        //        ImagePath = "/app/publish",
        //        TargetStage = "publish",
        //        File = Path.Combine(".", lambdaFunction.Name, "Dockerfile"),
        //        BuildArgs = new Dictionary<string, string>
        //                {
        //                    { "mainProject", lambdaFunction.Name }
        //                }
        //    })
        //});
    }
}