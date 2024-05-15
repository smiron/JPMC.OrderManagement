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
    public ApiContainerSettings ApiContainer { get; set; } = new();
}

public class ApiContainerSettings
{
    public string Tag { get; set; } = "latest";

    public int CPU { get; set; } = 1024;

    public int Memory { get; set; } = 2048;
}

public class LoadBalancerAppSettings
{
    public string? RestrictIngressToCidr { get; set; } = null;
}