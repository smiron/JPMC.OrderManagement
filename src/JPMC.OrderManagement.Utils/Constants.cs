namespace JPMC.OrderManagement.Utils;

public static class Constants
{
    public const string Owner = "JPMC";
    public const string System = "OrderManagement";

    public const string SolutionNameId = "jpmc-order-management";
    public static readonly string SolutionName = $"{Owner}.{System}";
    public static readonly string SolutionNameToLower = SolutionName.ToLowerInvariant();

    public const string ComputeEnvironmentVariablesPrefix = "JPMC_OM_";

    public static readonly string EnvironmentVariableName = $"{ComputeEnvironmentVariablesPrefix}ENVIRONMENT";
}