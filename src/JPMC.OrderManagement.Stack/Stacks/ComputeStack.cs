using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.Batch;
using Constructs;

using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using JPMC.OrderManagement.Common;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;

using AmazonCDK = Amazon.CDK;
using DDB = Amazon.CDK.AWS.DynamoDB;
using EventBus = Amazon.CDK.AWS.Events.EventBus;
using EventBusProps = Amazon.CDK.AWS.Events.EventBusProps;
using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;

namespace JPMC.OrderManagement.Stack.Stacks;

internal sealed class ComputeStack : AmazonCDK.Stack
{
    private readonly DDB.EnableScalingProps _dynamoDbAutoScaling = new() 
    {
        MinCapacity = 1,
        MaxCapacity = 10
    };

    private readonly DDB.UtilizationScalingProps _dynamoDbUtilizationScalingProps = new()
    {
        TargetUtilizationPercent = 80
    };
    
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
            PointInTimeRecovery = false,
            DeletionProtection = true,
            ContributorInsightsEnabled = false,
            TimeToLiveAttribute = "TTL",
            TableClass = DDB.TableClass.STANDARD
        });

        var ddbTableGsi1Props = new DDB.GlobalSecondaryIndexProps
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
            ProjectionType = DDB.ProjectionType.ALL
        };
        ddbTable.AddGlobalSecondaryIndex(ddbTableGsi1Props);
        
        ddbTable.AutoScaleReadCapacity(_dynamoDbAutoScaling).ScaleOnUtilization(_dynamoDbUtilizationScalingProps);
        ddbTable.AutoScaleWriteCapacity(_dynamoDbAutoScaling).ScaleOnUtilization(_dynamoDbUtilizationScalingProps);

        ddbTable.AutoScaleGlobalSecondaryIndexReadCapacity(ddbTableGsi1Props.IndexName, _dynamoDbAutoScaling).ScaleOnUtilization(_dynamoDbUtilizationScalingProps);
        ddbTable.AutoScaleGlobalSecondaryIndexWriteCapacity(ddbTableGsi1Props.IndexName, _dynamoDbAutoScaling).ScaleOnUtilization(_dynamoDbUtilizationScalingProps);
        
        var ecsApiTaskLogGroup = new LogGroup(this, "ecs-api-task-log-group", new LogGroupProps
        {
            LogGroupName = $"/{Constants.Owner}/{Constants.System}/API",
            RemovalPolicy = AmazonCDK.RemovalPolicy.DESTROY,
            Retention = RetentionDays.ONE_WEEK,
            LogGroupClass = LogGroupClass.STANDARD
        });

        var batchDataLoaderTaskLogGroup = new LogGroup(this, "batch-data-loader-task-log-group", new LogGroupProps
        {
            LogGroupName = $"/{Constants.Owner}/{Constants.System}/DataLoader",
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
        
        ecsCluster.AddDefaultCapacityProviderStrategy([new CapacityProviderStrategy
        {
            CapacityProvider = "FARGATE_SPOT"
        }]);

        var ecsApiTask = new FargateTaskDefinition(this, "ecs-task-api", new FargateTaskDefinitionProps
        {
            Family = $"{Constants.SolutionNameId}-api",
            MemoryLimitMiB = appSettings.Service.ApiContainer.Memory,
            Cpu = appSettings.Service.ApiContainer.CPU,
            RuntimePlatform = new RuntimePlatform
            {
                CpuArchitecture = CpuArchitecture.X86_64,
                OperatingSystemFamily = OperatingSystemFamily.LINUX
            }
        });

        var dynamoDbIamPolicy = new Policy(this, "dynamodb-iam-policy", new PolicyProps
        {
            PolicyName = $"DynamoDB-{ddbTable.TableName}",
            Statements =
            [
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "AllowActions",
                    Effect = Effect.ALLOW,
                    Actions =
                    [
                        "dynamodb:GetItem", "dynamodb:DeleteItem", "dynamodb:UpdateItem", "dynamodb:PutItem",
                        "dynamodb:Query", "dynamodb:DescribeTable", "dynamodb:BatchWriteItem"
                    ],
                    Resources = [ddbTable.TableArn, $"{ddbTable.TableArn}/index/{ddbTableGsi1Props.IndexName}"]
                }),
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "DenyActions",
                    Effect = Effect.DENY,
                    Actions = ["dynamodb:Scan"],
                    Resources = [ddbTable.TableArn, $"{ddbTable.TableArn}/index/{ddbTableGsi1Props.IndexName}"]
                })
            ]
        });

        var cloudWatchLogsIamPolicy = new Policy(this, "cloudwatch-logs-iam-policy", new PolicyProps
        {
            PolicyName = "CloudWatchLogs",
            Statements =
            [
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "AllowActions",
                    Effect = Effect.ALLOW,
                    Actions =
                    [
                        "logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents", "logs:DescribeLogGroups"
                    ],
                    Resources = [$"arn:{Partition}:logs:{Region}:{Account}:*"]
                })
            ]
        });

        ecsApiTask.TaskRole.AttachInlinePolicy(dynamoDbIamPolicy);
        ecsApiTask.TaskRole.AttachInlinePolicy(cloudWatchLogsIamPolicy);

        ecsApiTask.AddContainer("api", new ContainerDefinitionOptions
        {
            ContainerName = "api",
            Image = ContainerImage.FromDockerImageAsset(ciCdStack.ApiDockerImageAsset),
            MemoryLimitMiB = appSettings.Service.ApiContainer.Memory,
            Cpu = appSettings.Service.ApiContainer.CPU,
            ReadonlyRootFilesystem = true,
            Environment = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", appSettings.Environment },
                { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__DynamoDbTableName", Constants.SolutionNameToLower },
                { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__HttpLogging", "false" },
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
            ]
        });

        var ecsService = new FargateService(this, "ecs-service", new FargateServiceProps
        {
            ServiceName = Constants.SolutionNameId,
            Cluster = ecsCluster,
            PlatformVersion = FargatePlatformVersion.LATEST,
            SecurityGroups = [ networkingStack.ComputeSecurityGroup ],
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            TaskDefinition = ecsApiTask,
            AssignPublicIp = false,
            CircuitBreaker = new DeploymentCircuitBreaker
            {
                Enable = true,
                Rollback = true
            },
            CapacityProviderStrategies = [new CapacityProviderStrategy
            {
                CapacityProvider = "FARGATE_SPOT",
                Weight = 100
            }]
        });

        ecsService.AutoScaleTaskCount(new EnableScalingProps
        {
            MinCapacity = appSettings.Service.ApiContainer.MinInstanceCount,
            MaxCapacity = appSettings.Service.ApiContainer.MaxInstanceCount
        });
        
        ecsService.AttachToApplicationTargetGroup(networkingStack.AlbTargetGroup);

        // Blob import related infrastructure
        var s3Bucket = new Bucket(this, "bucket", new BucketProps
        {
            BucketName = $"{Constants.SolutionNameId}-{appSettings.Environment}",
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            EnforceSSL = true,
            ObjectOwnership = ObjectOwnership.BUCKET_OWNER_ENFORCED,
            PublicReadAccess = false,
            Encryption = BucketEncryption.S3_MANAGED,
            RemovalPolicy = AmazonCDK.RemovalPolicy.DESTROY
        });

        var batchEnvironment = new FargateComputeEnvironment(this, "batch-environment",
            new FargateComputeEnvironmentProps
            {
                ComputeEnvironmentName = $"{Constants.SolutionNameId}-{appSettings.Environment}",
                Enabled = true,
                MaxvCpus = 1,
                Spot = true,
                Vpc = networkingStack.Vpc,
                SecurityGroups = [networkingStack.ComputeSecurityGroup],
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
                UpdateToLatestImageVersion = true
            });

        var jobQueue = new JobQueue(this, "batch-queue", new JobQueueProps
        {
            JobQueueName = $"{Constants.SolutionNameId}-{appSettings.Environment}-data-loader",
            Enabled = true,
            ComputeEnvironments = [new OrderedComputeEnvironment
            {
                ComputeEnvironment = batchEnvironment
            }]
        });

        var ecsDataLoaderVolume = EcsVolume.Host(new HostVolumeOptions
        {
            Name = "data",
            ContainerPath = "/app/data",
            Readonly = false
        });

        const string dataLoaderJobObjectKeyParameterName = "objectKey";
        var batchJobDefinition = new EcsJobDefinition(this, "data-loader-job-definition", new EcsJobDefinitionProps
        {
            JobDefinitionName = $"{Constants.SolutionNameId}-{appSettings.Environment}-data-loader",
            Timeout = AmazonCDK.Duration.Minutes(1),
            Parameters = new Dictionary<string, object>
            {
                { dataLoaderJobObjectKeyParameterName, "sample-data.csv" }
            },
            Container = new EcsFargateContainerDefinition(this, "data-loader-job-container-definition",
                new EcsFargateContainerDefinitionProps
                {
                    AssignPublicIp = false,
                    Image = ContainerImage.FromDockerImageAsset(ciCdStack.DataLoaderDockerImageAsset),
                    Cpu = appSettings.Service.DataLoaderContainer.CPU / 1024.0,
                    Memory = AmazonCDK.Size.Mebibytes(appSettings.Service.DataLoaderContainer.Memory),
                    Environment = new Dictionary<string, string>
                    {
                        { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__Environment", appSettings.Environment },
                        { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__DynamoDbTableName", Constants.SolutionNameToLower },
                        { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__DownloadToFile", $"{ecsDataLoaderVolume.ContainerPath}/data.csv" },
                        { $"{Constants.ComputeEnvironmentVariablesPrefix}Service__Job__BucketName", s3Bucket.BucketName },
                        { $"{Constants.ComputeEnvironmentVariablesPrefix}CloudWatchLogs__Enable", "true" },
                        { $"{Constants.ComputeEnvironmentVariablesPrefix}CloudWatchLogs__LogGroup", batchDataLoaderTaskLogGroup.LogGroupName }
                    },
                    JobRole = new Role(this, "data-loader-job-role", new RoleProps
                    {
                        RoleName = $"{Constants.SolutionNameId}-{appSettings.Environment}-data-loader",
                        AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com", new ServicePrincipalOpts
                        {
                            Region = Region
                        })
                    }),
                    ReadonlyRootFilesystem = true,
                    Command = ["--Service:Job:ObjectKey", $"Ref::{dataLoaderJobObjectKeyParameterName}"]
                }),
        });
        
        batchJobDefinition.Container.AddVolume(ecsDataLoaderVolume);

        s3Bucket.GrantRead(batchJobDefinition.Container.JobRole!);
        batchJobDefinition.Container.JobRole!.AttachInlinePolicy(dynamoDbIamPolicy);
        batchJobDefinition.Container.JobRole!.AttachInlinePolicy(cloudWatchLogsIamPolicy);
        
        s3Bucket.EnableEventBridgeNotification();

        var eventBus = new EventBus(this, "event-bus", new EventBusProps
        {
            EventBusName = $"{Constants.SolutionNameId}-{appSettings.Environment}"
        });

        var defaultEventBusToSolutionEventBusS3 = new Rule(this, "default-solution-event-bus-s3", new RuleProps
        {
            RuleName = $"{Constants.SolutionNameId}-{appSettings.Environment}-default-to-solution-event-bus-s3",
            Enabled = true,
            EventPattern = new EventPattern
            {
                Source = ["aws.s3"],
                Detail = new Dictionary<string, object>
                {
                    {
                        "bucket", new Dictionary<string, object>
                        {
                            { "name", new[] { s3Bucket.BucketName } }
                        }
                    }
                }
            },
            EventBus = EventBus.FromEventBusName(this, "default-event-bus", "default"),
            Targets =
            [
                new Amazon.CDK.AWS.Events.Targets.EventBus(
                    eventBus,
                    new Amazon.CDK.AWS.Events.Targets.EventBusProps
                    {
                        DeadLetterQueue = new Queue(this, "default-solution-event-bus-s3-dlq", new QueueProps
                        {
                            QueueName = $"{Constants.SolutionNameId}-{appSettings.Environment}-default-to-solution-event-bus-s3-dlq"
                        })
                    })
            ]
        });

        var s3ToBatchRule = new Rule(this, "s3-object-created-to-batch", new RuleProps
        {
            RuleName = $"{Constants.SolutionNameId}-{appSettings.Environment}-s3-object-created-to-batch",
            Enabled = true,
            EventBus = eventBus,
            EventPattern = new EventPattern
            {
                Source = ["aws.s3"],
                DetailType = ["Object Created"],
                Detail = new Dictionary<string, object>
                {
                    {
                        "bucket", new Dictionary<string, object>
                        {
                            { "name", new[] { s3Bucket.BucketName } }
                        }
                    }
                }
            },
            Targets =
            [
                new BatchJob(
                    jobQueue.JobQueueArn,
                    this,
                    batchJobDefinition.JobDefinitionArn,
                    this,
                    new BatchJobProps
                    {
                        JobName = $"{Constants.SolutionNameId}-{appSettings.Environment}-data-loader",
                        Event = RuleTargetInput.FromObject(new Dictionary<string, object>
                        {
                            {
                                "Parameters", new Dictionary<string, object>
                                {
                                    { dataLoaderJobObjectKeyParameterName, EventField.FromPath("$.detail.object.key") }
                                }
                            }
                        }),
                        RetryAttempts = 2,
                        MaxEventAge = AmazonCDK.Duration.Hours(2),
                        DeadLetterQueue = new Queue(this, "s3-object-created-to-dlq", new QueueProps
                        {
                            QueueName = $"{Constants.SolutionNameId}-{appSettings.Environment}-s3-object-created-to-dlq"
                        })
                    })
            ]
        });
    }
}