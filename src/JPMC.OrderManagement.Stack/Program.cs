using Amazon.CDK;
using JPMC.OrderManagement.Stack.Stacks;

using CdkTags = Amazon.CDK.Tags;

namespace JPMC.OrderManagement.Stack;

static class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        var infrastructureStack = new InfrastructureStack(app);

        CdkTags.Of(infrastructureStack).Add("user:solution", Constants.SolutionName);

        app.Synth();
    }
}