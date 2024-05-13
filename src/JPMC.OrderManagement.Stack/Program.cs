using Microsoft.Extensions.Configuration;

using Amazon.CDK;
using JPMC.OrderManagement.Stack.Stacks;
using JPMC.OrderManagement.Utils;

using CdkTags = Amazon.CDK.Tags;
using Environment = Amazon.CDK.Environment;

namespace JPMC.OrderManagement.Stack;

static class Program
{
    public static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .AddEnvironmentVariables(source => source.Prefix = Constants.ComputeEnvironmentVariablesPrefix)
            .Build();

        var appSettings = configuration
            .Get<AppSettings>(options =>
            {
                options.ErrorOnUnknownConfiguration = true;
            })!;

        var app = new App();

        var stackProps = new StackProps
        {
            Env = new Environment
            {
                Region = appSettings.Region
            }
        };

        var infrastructureStack = new InfrastructureStack(app, stackProps);

        CdkTags.Of(infrastructureStack).Add($"user:{nameof(Constants.Owner)}", Constants.Owner);
        CdkTags.Of(infrastructureStack).Add($"user:{nameof(Constants.System)}", Constants.System);

        app.Synth();
    }
}