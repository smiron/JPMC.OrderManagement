using Amazon.CDK.AWS.Batch;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.EKS;
using Constructs;
using JPMC.OrderManagement.Utils;
using AmazonCDK = Amazon.CDK;
using Cluster = Amazon.CDK.AWS.ECS.Cluster;
using ClusterProps = Amazon.CDK.AWS.ECS.ClusterProps;
using DDB = Amazon.CDK.AWS.DynamoDB;

namespace JPMC.OrderManagement.Stack.Stacks;

public class CiCdStack : AmazonCDK.Stack
{
    internal CiCdStack(Construct scope, AppSettings appSettings, NetworkingStack networkingStack,
        AmazonCDK.IStackProps? stackProps = null)
        : base(scope,
            $"{Constants.Owner}-{Constants.System}-{nameof(CiCdStack)}",
            stackProps)
    {
        JpmcOrderManagementApiRepository = new Repository(this, "ecr", new RepositoryProps
        {
            RepositoryName = $"{Constants.SolutionNameId}-api",
            ImageScanOnPush = true,
            EmptyOnDelete = true,
            RemovalPolicy = AmazonCDK.RemovalPolicy.DESTROY
        });
    }

    public Repository JpmcOrderManagementApiRepository { get; private set; }
}