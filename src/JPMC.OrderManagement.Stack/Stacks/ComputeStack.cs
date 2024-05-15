using Amazon.CDK.AWS.Batch;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.EKS;
using Amazon.CDK.AWS.Logs;
using Constructs;
using JPMC.OrderManagement.Utils;
using AmazonCDK = Amazon.CDK;
using Cluster = Amazon.CDK.AWS.ECS.Cluster;
using ClusterProps = Amazon.CDK.AWS.ECS.ClusterProps;
using DDB = Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;

namespace JPMC.OrderManagement.Stack.Stacks;

internal class ComputeStack : AmazonCDK.Stack
{
    internal ComputeStack(
        Construct scope, 
        AppSettings appSettings,
        NetworkingStack networkingStack,
        CiCdStack ciCdStack,
        AmazonCDK.IStackProps? stackProps = null)
        : base(scope, 
            $"{Constants.Owner}-{Constants.System}-{nameof(ComputeStack)}",
            stackProps)
    {
        var genericResourceName = $"{Constants.Owner}-{Constants.System}".ToLower();
        
        var ddbTable = new DDB.Table(this, "order-management-data", new DDB.TableProps
        {
            TableName = $"{appSettings.Environment}.{Constants.SolutionNameToLower}",
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

        ddbTable.AddGlobalSecondaryIndex(new DDB.GlobalSecondaryIndexProps
        {
            IndexName = "GSI1",
            PartitionKey = new DDB.Attribute
            {
                Name = "GSI1PK",
                Type = DDB.AttributeType.STRING
            },
            SortKey = new DDB.Attribute
            {
                Name = "GSI1SK",
                Type = DDB.AttributeType.STRING
            },
            ProjectionType = DDB.ProjectionType.ALL,
            ReadCapacity = 1,
            WriteCapacity = 1
        });

        var ecsApiTaskLogGroup = new LogGroup(this, "ecs-api-task-log-group", new LogGroupProps
        {
            LogGroupName = $"/{Constants.Owner}/{Constants.System}/API",
            RemovalPolicy = AmazonCDK.RemovalPolicy.DESTROY,
            Retention = RetentionDays.ONE_WEEK,
            LogGroupClass = LogGroupClass.STANDARD
        });

        var ecsCluster = new Cluster(this, "ecs-fargate", new ClusterProps
        {
            ClusterName = genericResourceName,
            Vpc = networkingStack.Vpc,
            EnableFargateCapacityProviders = true,
            ContainerInsights = true
        });

        var ecsApiTask = new FargateTaskDefinition(this, "ecs-task-api", new FargateTaskDefinitionProps
        {
            Family = $"{Constants.SolutionNameId}-service-api",
            MemoryLimitMiB = 512,
            Cpu = 256,
            RuntimePlatform = new RuntimePlatform
            {
                CpuArchitecture = CpuArchitecture.X86_64,
                OperatingSystemFamily = OperatingSystemFamily.LINUX
            }
        });

        ecsApiTask.TaskRole.AttachInlinePolicy(new Policy(this, "ecs-task-role-ddb", new PolicyProps
        {
            PolicyName = $"DynamoDB-{ddbTable.TableName}",
            Statements =
            [
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "AllowActions",
                    Effect = Effect.ALLOW,
                    Actions = ["dynamodb:*"],
                    Resources = [ddbTable.TableArn]
                }),
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "DenyActions",
                    Effect = Effect.DENY,
                    Actions = ["dynamodb:Scan"],
                    Resources = [ddbTable.TableArn]
                })
            ]
        }));

        ecsApiTask.TaskRole.AttachInlinePolicy(new Policy(this, "ecs-task-role-cloudwatch-logs", new PolicyProps
        {
            PolicyName = $"CloudWatchLogs-{Constants.Owner}{Constants.System}API",
            Statements =
            [
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "AllowActions",
                    Effect = Effect.ALLOW,
                    Actions = ["logs:CreateLogStream", "logs:PutLogEvents"],
                    Resources = [ecsApiTaskLogGroup.LogGroupArn]
                })
            ]
        }));

        ecsApiTask.AddContainer("api", new ContainerDefinitionOptions
        {
            ContainerName = "api",
            Image = ContainerImage.FromEcrRepository(ciCdStack.JpmcOrderManagementApiRepository, appSettings.Service.ContainerTag),
            MemoryLimitMiB = 512,
            Cpu = 256,
            ReadonlyRootFilesystem = true,
            Environment = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", appSettings.Environment },
                { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__DynamoDbTableName", Constants.SolutionNameToLower },
                { $"{Constants.ComputeEnvironmentVariablesPrefix}CloudWatchLogs__Enable", "true" },
                { $"{Constants.ComputeEnvironmentVariablesPrefix}CloudWatchLogs__LogGroup", ecsApiTaskLogGroup.LogGroupName },
                { $"{Constants.ComputeEnvironmentVariablesPrefix}XRay__Enable", "false" },
            },
            PortMappings = 
            [
                new PortMapping
                {
                    Name = $"api-{NetworkingStack.ApplicationPort}-tcp",
                    AppProtocol = AppProtocol.Http,
                    ContainerPort = NetworkingStack.ApplicationPort,
                    HostPort = NetworkingStack.ApplicationPort,
                    Protocol = Amazon.CDK.AWS.ECS.Protocol.TCP
                }
            ],
            Logging = new AwsLogDriver(new AwsLogDriverProps
            {
                LogRetention = RetentionDays.ONE_WEEK,
                Mode = AwsLogDriverMode.NON_BLOCKING,
                StreamPrefix = "ecs-log-driver-",
                // LogGroup = 
            })
        });

        var ecsService = new FargateService(this, "ecs-service", new FargateServiceProps
        {
            Cluster = ecsCluster,
            DesiredCount = 1,
            PlatformVersion = FargatePlatformVersion.LATEST,
            ServiceName = Constants.SolutionNameId,
            SecurityGroups = [ networkingStack.ComputeSecurityGroup ],
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            TaskDefinition = ecsApiTask,
            AssignPublicIp = false
        });
    }
}