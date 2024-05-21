using Constructs;

using AmazonCDK = Amazon.CDK;
using Amazon.CDK.AWS.ECR;
using JPMC.OrderManagement.Common;

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

        JpmcOrderManagementDataLoaderRepository = new Repository(this, "ecr-data-loader", new RepositoryProps
        {
            RepositoryName = $"{Constants.SolutionNameId}-data-loader",
            ImageScanOnPush = true,
            EmptyOnDelete = true,
            RemovalPolicy = AmazonCDK.RemovalPolicy.DESTROY
        });
    }

    public Repository JpmcOrderManagementApiRepository { get; private set; }
    
    public Repository JpmcOrderManagementDataLoaderRepository { get; private set; }
}