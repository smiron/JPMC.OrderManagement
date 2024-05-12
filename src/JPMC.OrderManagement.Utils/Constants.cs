namespace JPMC.OrderManagement.Utils;

public static class Constants
{
    public static string SolutionName = "JPMC.OrderManagement";
    public static readonly string SolutionNameToLower = SolutionName.ToLowerInvariant();

    public const string ComputeEnvironmentVariablesPrefix = "JPMC_OM_";

    public static readonly string EnvironmentVariableName = $"{ComputeEnvironmentVariablesPrefix}ENVIRONMENT";
}