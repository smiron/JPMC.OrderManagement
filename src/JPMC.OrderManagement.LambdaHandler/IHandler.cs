using Amazon.Lambda.Core;

namespace JPMC.OrderManagement.LambdaHandler;

public interface IHandler<in TInput, TOutput, in TAppSettings>
{
    void Initialize(TAppSettings appSettings);

    Task<TOutput> Handle(TInput command, ILambdaContext context);
}