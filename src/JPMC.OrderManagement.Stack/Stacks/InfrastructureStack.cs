using Amazon.CDK.AWS.IAM;
using Microsoft.Extensions.Configuration;

using Constructs;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using JPMC.OrderManagement.Utils;
using AmazonCDK = Amazon.CDK;
using DDB = Amazon.CDK.AWS.DynamoDB;

namespace JPMC.OrderManagement.Stack.Stacks;

internal class InfrastructureStack : AmazonCDK.Stack
{
    private readonly AppSettings _appSettings;

    internal InfrastructureStack(Construct scope, AmazonCDK.IStackProps? stackProps = null)
        : base(scope, 
            $"{Constants.SolutionName}.{nameof(InfrastructureStack)}".Replace(".", "-"),
            stackProps)
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

        var functionId = "order-management";
        
        var ddbTable = new DDB.Table(this, "order-management-data", new DDB.TableProps
        {
            TableName = $"{_appSettings.Environment}.{Constants.SolutionNameToLower}",
            PartitionKey = new DDB.Attribute
            {
                Name = "PK",
                Type = DDB.AttributeType.STRING
            },
            SortKey = new DDB.Attribute
            {
                Name = "SK",
                Type = DDB.AttributeType.STRING
            },
            RemovalPolicy = AmazonCDK.RemovalPolicy.DESTROY,
            Encryption = DDB.TableEncryption.AWS_MANAGED,
            BillingMode = DDB.BillingMode.PROVISIONED,
            ReadCapacity = 1,
            WriteCapacity = 1,
            PointInTimeRecovery = false,
            DeletionProtection = true,
            ContributorInsightsEnabled = false,
            TimeToLiveAttribute = "TTL",
            TableClass = DDB.TableClass.STANDARD
        });

        var fn = new Function(this, functionId, new FunctionProps
        {
            FunctionName = $"{_appSettings.Environment}-{functionId}",
            Runtime = Runtime.DOTNET_8,
            Architecture = Architecture.X86_64,
            MemorySize = 512,
            Environment = new Dictionary<string, string>
            {
                {
                    Constants.EnvironmentVariableName, _appSettings.Environment
                },
                {
                    "POWERTOOLS_SERVICE_NAME", functionId
                },
                {
                    "POWERTOOLS_LOG_LEVEL", "Info"
                },
                {
                    "POWERTOOLS_LOGGER_LOG_EVENT", "true"
                },
                {
                    "POWERTOOLS_LOGGER_CASE", "CamelCase"
                },
                {
                    "POWERTOOLS_LOGGER_SAMPLE_RATE", "1"
                }
            },

            Timeout = AmazonCDK.Duration.Seconds(30),
            LogRetention = RetentionDays.ONE_WEEK,

            // functions using top level statements can just specify the main DLL name as the handler
            Handler = "JPMC.OrderManagement.Lambda",
            
            Code = Code.FromDockerBuild("src", new DockerBuildAssetOptions
            {
                Platform = "linux/amd64",
                ImagePath = "/app/publish",
                TargetStage = "publish",
                File = Path.Combine(".", "JPMC.OrderManagement.Lambda", "Dockerfile"),
                BuildArgs = new Dictionary<string, string>
                {
                    { "mainProject", "JPMC.OrderManagement.Lambda" }
                }
            })
        });

        var functionId2 = $"{functionId}-2";
        var fn2 = new Function(this, functionId2, new FunctionProps
        {
            FunctionName = $"{_appSettings.Environment}-{functionId2}",
            Runtime = Runtime.DOTNET_8,
            Architecture = Architecture.X86_64,
            MemorySize = 512,
            Environment = new Dictionary<string, string>
            {
                {
                    Constants.EnvironmentVariableName, _appSettings.Environment
                },
                {
                    "POWERTOOLS_SERVICE_NAME", functionId2
                },
                {
                    "POWERTOOLS_LOG_LEVEL", "Info"
                },
                {
                    "POWERTOOLS_LOGGER_LOG_EVENT", "true"
                },
                {
                    "POWERTOOLS_LOGGER_CASE", "CamelCase"
                },
                {
                    "POWERTOOLS_LOGGER_SAMPLE_RATE", "1"
                }
            },

            Timeout = AmazonCDK.Duration.Seconds(30),
            LogRetention = RetentionDays.ONE_WEEK,

            // functions using top level statements can just specify the main DLL name as the handler
            Handler = "JPMC.OrderManagement.Lambda2",

            Code = Code.FromDockerBuild("src", new DockerBuildAssetOptions
            {
                Platform = "linux/amd64",
                ImagePath = "/app/publish",
                TargetStage = "publish",
                File = Path.Combine(".", "JPMC.OrderManagement.Lambda2", "Dockerfile"),
                BuildArgs = new Dictionary<string, string>
                {
                    { "mainProject", "JPMC.OrderManagement.Lambda2" }
                }
            })
        });

        ddbTable.GrantReadWriteData(fn.GrantPrincipal);
        
        fn.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "DynamoDBDeny",
            Actions = new[]
            {
                "dynamodb:Scan"
            },
            Effect = Effect.DENY,
            Resources = new[] { "*" }
        }));

        ddbTable.GrantReadWriteData(fn2.GrantPrincipal);

        fn2.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "DynamoDBDeny",
            Actions = new[]
            {
                "dynamodb:Scan"
            },
            Effect = Effect.DENY,
            Resources = new[] { "*" }
        }));
    }
}