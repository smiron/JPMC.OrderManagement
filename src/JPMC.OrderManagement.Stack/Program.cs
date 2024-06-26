﻿using Microsoft.Extensions.Configuration;

using Amazon.CDK;
using JPMC.OrderManagement.Common;
using JPMC.OrderManagement.Stack.Stacks;

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

        var networkingStack = new NetworkingStack(app, appSettings, stackProps);
        var ciCdStack = new CiCdStack(app, appSettings, networkingStack, stackProps);
        var computeStack = new ComputeStack(app, appSettings, networkingStack, ciCdStack, stackProps);

        computeStack.AddDependency(ciCdStack);
        computeStack.AddDependency(networkingStack);

        foreach (var stack in new Amazon.CDK.Stack[]{ networkingStack, ciCdStack, computeStack })
        {
            CdkTags.Of(stack).Add($"user:{nameof(Constants.Owner)}", Constants.Owner);
            CdkTags.Of(stack).Add($"user:{nameof(Constants.System)}", Constants.System);
            CdkTags.Of(stack).Add("user:Environment", appSettings.Environment);
        }

        app.Synth();
    }
}