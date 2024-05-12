using Amazon.Lambda.Core;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Tracing;

namespace JPMC.OrderManagement.LambdaHandler;

public abstract class LambdaHandlerBase<TInput, TOutput, TAppSettings> : IHandler<TInput, TOutput, TAppSettings>
{
    private const int RemainingTimeThresholdSeconds = 5;

    private bool _isInitialized;
    protected TAppSettings? AppSettings;

    public void Initialize(TAppSettings appSettings)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("The handler has already been initialized.");
        }

        AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _isInitialized = true;

        InitializeInternal();
    }

    protected virtual void InitializeInternal()
    {
    }

    [Logging(LogEvent = true)]
    [Tracing(SegmentName = "Handle")]
    public async Task<TOutput> Handle(TInput command, ILambdaContext context)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The handler has not been initialized.");
        }

        try
        {
            return await HandleInternal(command, context);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }

    protected abstract Task<TOutput> HandleInternal(TInput command, ILambdaContext context);

    protected static bool LambdaHasTime(ILambdaContext context)
    {
        var ret = context.RemainingTime.TotalSeconds > RemainingTimeThresholdSeconds;

        if (ret == false)
        {
            Logger.LogWarning("Lambda function ran out of time.");
        }

        return ret;
    }
}