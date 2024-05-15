namespace JPMC.OrderManagement.Stack;

public class AppSettings
{
    public string Region { get; set; } = "eu-west-2";
    
    public string Environment { get; set; } = null!;

    public ServiceAppSettings Service { get; set; } = new ();

    public LoadBalancerAppSettings LoadBalancer { get; set; } = new ();
}

public class ServiceAppSettings
{
    public string ContainerTag { get; set; } = "latest";
}

public class LoadBalancerAppSettings
{
    public string? RestrictIngressToCidr { get; set; } = null;
}