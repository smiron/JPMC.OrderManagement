using Constructs;

using Amazon.CDK.AWS.Ecr.Assets;

using AmazonCDK = Amazon.CDK;

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
        ApiDockerImageAsset = new DockerImageAsset(this, "api", new DockerImageAssetProps
        {
            Directory = ".",
            Target = "restapi",
            File = "Dockerfile",
            BuildArgs = new Dictionary<string, string>
            {
                { "mainProject", "JPMC.OrderManagement.API" }
            },
            AssetName = "jpmc-order-management-api",
            IgnoreMode = AmazonCDK.IgnoreMode.DOCKER,
            Platform = Platform_.LINUX_AMD64
        });

        DataLoaderDockerImageAsset = new DockerImageAsset(this, "data-loader", new DockerImageAssetProps
        {
            Directory = ".",
            Target = "service",
            File = "Dockerfile",
            BuildArgs = new Dictionary<string, string>
            {
                { "mainProject", "JPMC.OrderManagement.DataLoader.Service" }
            },
            AssetName = "jpmc-order-management-data-loader",
            IgnoreMode = AmazonCDK.IgnoreMode.DOCKER,
            Platform = Platform_.LINUX_AMD64
        });
    }
    
    public DockerImageAsset ApiDockerImageAsset { get; private set; }
    
    public DockerImageAsset DataLoaderDockerImageAsset { get; private set; }
}