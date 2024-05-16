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
    public ContainerSettings ApiContainer { get; set; } = new();
    
    public ContainerSettings DataLoaderContainer { get; set; } = new();
}

public class ContainerSettings
{
    public string Tag { get; set; } = "latest";

    // ReSharper disable once InconsistentNaming
    public double CPU { get; set; }

    public int Memory { get; set; } = 2048;
}

public class LoadBalancerAppSettings
{
    public string? RestrictIngressToCidr { get; set; } = null;
}