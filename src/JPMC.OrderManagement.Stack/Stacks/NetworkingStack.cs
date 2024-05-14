using Constructs;
using JPMC.OrderManagement.Utils;

using AmazonCDK = Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;

namespace JPMC.OrderManagement.Stack.Stacks;

internal sealed class NetworkingStack : AmazonCDK.Stack
{
    internal NetworkingStack(Construct scope, AppSettings appSettings, AmazonCDK.IStackProps? stackProps = null)
        : base(scope,
            $"{Constants.Owner}-{Constants.System}-{nameof(NetworkingStack)}",
            stackProps)
    {
        var vpc = new Vpc(this, "vpc", new VpcProps
        {
            VpcName = Constants.SolutionName,
            EnableDnsHostnames = true,
            EnableDnsSupport = true,
            CreateInternetGateway = false,
            IpAddresses = IpAddresses.Cidr("172.31.0.0/16"),
            MaxAzs = 3,
            SubnetConfiguration =
            [
                new SubnetConfiguration
                {
                    Name = "Private",
                    CidrMask = 20,
                    SubnetType = SubnetType.PRIVATE_ISOLATED
                }
            ]
        });

        var loadBalancerSecurityGroup = new SecurityGroup(this, "load-balancer-sg", new SecurityGroupProps
        {
            Vpc = vpc,
            SecurityGroupName = "load-balancer",
            AllowAllOutbound = false,
            AllowAllIpv6Outbound = false
        });

        var computeSecurityGroup = new SecurityGroup(this, "compute-sg", new SecurityGroupProps
        {
            Vpc = vpc,
            SecurityGroupName = "load-balancer",
            AllowAllOutbound = false,
            AllowAllIpv6Outbound = false
        });

        computeSecurityGroup.AddIngressRule(loadBalancerSecurityGroup, Port.Tcp(8080), "Allow 8080 from Load Balancer");

        //vpc.SelectSubnets(new SubnetSelection
        //{
        //    SubnetType = SubnetType.PUBLIC
        //})

        //var alb = new ApplicationLoadBalancer(this, "load-balancer", new ApplicationLoadBalancerProps
        //{
        //    Vpc = vpc,
        //    LoadBalancerName = Constants.SolutionName,
        //    SecurityGroup = loadBalancerSecurityGroup,
        //    InternetFacing = true,
        //    IpAddressType = IpAddressType.IPV4,
            
        //});
    }
}