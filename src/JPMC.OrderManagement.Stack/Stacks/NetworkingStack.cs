using Constructs;

using AmazonCDK = Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using JPMC.OrderManagement.Common;
using Protocol = Amazon.CDK.AWS.ElasticLoadBalancingV2.Protocol;

namespace JPMC.OrderManagement.Stack.Stacks;

internal sealed class NetworkingStack : AmazonCDK.Stack
{
    public const int InternetPort = 80;
    public const int ApplicationPort = 8080;

    private readonly ApplicationProtocol ApplicationProtocol = ApplicationProtocol.HTTP;

    internal NetworkingStack(Construct scope, AppSettings appSettings, AmazonCDK.IStackProps? stackProps = null)
        : base(scope,
            $"{Constants.Owner}-{Constants.System}-{nameof(NetworkingStack)}",
            stackProps)
    {
        Vpc = new Vpc(this, "vpc", new VpcProps
        {
            VpcName = Constants.SolutionName,
            EnableDnsHostnames = true,
            EnableDnsSupport = true,
            CreateInternetGateway = true,
            IpAddresses = IpAddresses.Cidr("172.31.0.0/16"),
            MaxAzs = 3,
            SubnetConfiguration =
            [
                new SubnetConfiguration
                {
                    Name = "Private",
                    CidrMask = 20,
                    SubnetType = SubnetType.PRIVATE_ISOLATED
                },
                new SubnetConfiguration
                {
                    Name = "Public",
                    CidrMask = 20,
                    SubnetType = SubnetType.PUBLIC
                }
            ]
        });
        
        LoadBalancerSecurityGroup = new SecurityGroup(this, "load-balancer-sg", new SecurityGroupProps
        {
            Vpc = Vpc,
            SecurityGroupName = "load-balancer-sg"
        });

        ComputeSecurityGroup = new SecurityGroup(this, "compute-sg", new SecurityGroupProps
        {
            Vpc = Vpc,
            SecurityGroupName = "compute-sg"
        });

        Vpc.AddInterfaceEndpoint("ecr-api-endpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.ECR,
            PrivateDnsEnabled = true,
            Subnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = [ComputeSecurityGroup],
            Open = true
        });

        Vpc.AddInterfaceEndpoint("ecr-dkr-endpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.ECR_DOCKER,
            PrivateDnsEnabled = true,
            Subnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = [ComputeSecurityGroup],
            Open = true
        });

        Vpc.AddInterfaceEndpoint("cloudwatch-logs-endpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.CLOUDWATCH_LOGS,
            PrivateDnsEnabled = true,
            Subnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = [ComputeSecurityGroup],
            Open = true
        });

        Vpc.AddInterfaceEndpoint("ec2-endpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.EC2,
            PrivateDnsEnabled = true,
            Subnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = [ComputeSecurityGroup],
            Open = true
        });

        Vpc.AddGatewayEndpoint("s3-gateway-endpoint", new GatewayVpcEndpointOptions
        {
            Service = GatewayVpcEndpointAwsService.S3,
            Subnets = [ new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED } ]
        });

        Vpc.AddGatewayEndpoint("ddb-gateway-endpoint", new GatewayVpcEndpointOptions
        {
            Service = GatewayVpcEndpointAwsService.DYNAMODB,
            Subnets = [new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED }]
        });

        ComputeSecurityGroup.AddIngressRule(
            LoadBalancerSecurityGroup,
            Port.Tcp(ApplicationPort),
            $"Allow {ApplicationPort} from Load Balancer");

        var alb = new ApplicationLoadBalancer(this, "load-balancer", new ApplicationLoadBalancerProps
        {
            Vpc = Vpc,
            LoadBalancerName = Constants.Owner,
            SecurityGroup = LoadBalancerSecurityGroup,
            InternetFacing = true,
            IpAddressType = IpAddressType.IPV4,
            DeletionProtection = true
        });

        AlbTargetGroup = new ApplicationTargetGroup(this, "ecs-target-group", new ApplicationTargetGroupProps
        {
            TargetGroupName = $"ecs-{Constants.Owner}-{Constants.System}",
            Vpc = Vpc,
            Port = ApplicationPort,
            Protocol = ApplicationProtocol,
            TargetType = TargetType.IP,
            ProtocolVersion = ApplicationProtocolVersion.HTTP1,
            HealthCheck = new HealthCheck
            {
                Enabled = true,
                Port = ApplicationPort.ToString(),
                Interval = AmazonCDK.Duration.Seconds(30),
                Path = "/api/health",
                Timeout = AmazonCDK.Duration.Seconds(5),
                UnhealthyThresholdCount = 2,
                HealthyThresholdCount = 5,
                Protocol = Protocol.HTTP
            }
        });

        var loadBalancerListener = alb.AddListener("listener-http", new ApplicationListenerProps
        {
            Protocol = ApplicationProtocol.HTTP,
            Port = InternetPort,
            DefaultAction = ListenerAction.Forward([AlbTargetGroup]),
            Open = false,
        });

        foreach (var restrictIngressToCidr in appSettings.LoadBalancer.RestrictIngressToCidrs)
        {
            loadBalancerListener.Connections.AllowDefaultPortFrom(Peer.Ipv4(restrictIngressToCidr),
                $"Allow port {InternetPort} from CIDR {restrictIngressToCidr}");
        }

        var albApiEndpointUrlOutput = new AmazonCDK.CfnOutput(this, "application-api-endpoint-http-url", new AmazonCDK.CfnOutputProps
        {
            ExportName = "Application-API-Endpoint-HTTP-URL",
            Key = "ApplicationApiEndpointHttpUrl",
            Description = "The application HTTP API endpoint",
            Value = $"{ApplicationProtocol}://{alb.LoadBalancerDnsName}/api"
        });

        var albSwaggerEndpointUrlOutput = new AmazonCDK.CfnOutput(this, "application-swagger-endpoint-http-url",
            new AmazonCDK.CfnOutputProps
            {
                ExportName = "Application-Swagger-Endpoint-HTTP-URL",
                Key = "ApplicationSwaggerEndpointHttpUrl",
                Description = "The application swagger API endpoint",
                Value = $"{ApplicationProtocol}://{alb.LoadBalancerDnsName}/api/swagger"
            });
    }

    public Vpc Vpc { get; private set; }

    public SecurityGroup ComputeSecurityGroup { get; private set; }
    
    public SecurityGroup LoadBalancerSecurityGroup { get; private set; }

    public ApplicationTargetGroup AlbTargetGroup { get; private set; }
}