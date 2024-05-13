namespace JPMC.OrderManagement.Utils;

public static class Constants
{
    public static string Owner = "JPMC";
    public static string System = "OrderManagement";

    public static readonly string SolutionName = $"{Owner}.{System}";
    public static readonly string SolutionNameToLower = SolutionName.ToLowerInvariant();

    public const string ComputeEnvironmentVariablesPrefix = "JPMC_OM_";

    public static readonly string EnvironmentVariableName = $"{ComputeEnvironmentVariablesPrefix}ENVIRONMENT";
}