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
    
    public ContainerSettings DataLoaderContainer { get; set; } = new();
}

public class ContainerSettings
{
    // ReSharper disable once InconsistentNaming
    public double CPU { get; set; } = 512;

    public int Memory { get; set; } = 1024;
}

public class ApiContainerSettings : ContainerSettings
{
    public int MinInstanceCount { get; set; } = 1;
    
    public int MaxInstanceCount { get; set; } = 1;
}

public class LoadBalancerAppSettings
{
    public string[] RestrictIngressToCidrs { get; set; } = new string[]{};
}