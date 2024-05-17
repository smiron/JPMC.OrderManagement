using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.Batch;
using Constructs;

using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using JPMC.OrderManagement.Common;
using AmazonCDK = Amazon.CDK;
using DDB = Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.S3;
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

        ecsApiTask.TaskRole.AttachInlinePolicy(new Policy(this, "ecs-task-role-ddb", new PolicyProps
        {
            PolicyName = $"DynamoDB-{ddbTable.TableName}",
            Statements =
            [
                new PolicyStatement(new PolicyStatementProps
                {
                    Sid = "AllowActions",
                    Effect = Effect.ALLOW,
                    Actions = ["dynamodb:GetItem", "dynamodb:DeleteItem", "dynamodb:UpdateItem", "dynamodb:PutItem", "dynamodb:Query", "dynamodb:DescribeTable"],
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
                    Actions = ["logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents", "logs:DescribeLogGroups"],
                    Resources = [$"arn:{Partition}:logs:{Region}:{Account}:*"]
                })
            ]
        }));

        ecsApiTask.AddContainer("api", new ContainerDefinitionOptions
        {
            ContainerName = "api",
            Image = ContainerImage.FromEcrRepository(ciCdStack.JpmcOrderManagementApiRepository, appSettings.Service.ApiContainer.Tag),
            MemoryLimitMiB = appSettings.Service.ApiContainer.Memory,
            Cpu = appSettings.Service.ApiContainer.CPU,
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
            } 
        });

        ecsService.AutoScaleTaskCount(new EnableScalingProps
        {
            MinCapacity = 1,
            MaxCapacity = 5
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

        var batchJobDefinition = new EcsJobDefinition(this, "data-loader-job-definition", new EcsJobDefinitionProps
        {
            JobDefinitionName = $"{Constants.SolutionNameId}-{appSettings.Environment}-data-loader",
            Timeout = AmazonCDK.Duration.Minutes(1),
            Container = new EcsFargateContainerDefinition(this, "data-loader-job-container-definition",
                new EcsFargateContainerDefinitionProps
                {
                    AssignPublicIp = false,
                    Image = ContainerImage.FromEcrRepository(ciCdStack.JpmcOrderManagementDataLoaderRepository, appSettings.Service.DataLoaderContainer.Tag),
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
                    ReadonlyRootFilesystem = true
                }),
        });
        
        batchJobDefinition.Container.AddVolume(ecsDataLoaderVolume);

        s3Bucket.GrantRead(batchJobDefinition.Container.JobRole!);
    }
}