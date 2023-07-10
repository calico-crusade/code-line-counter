namespace CodeLineCounter.Core.Base;

public abstract class BooleanVerb<T> : IVerb<T> where T : class
{
    public const string LOG_MESSAGE = "Executing: {name}";
    public readonly ILogger _logger;

    public virtual int ExitCodeSuccess => 0;

    public virtual int ExitCodeFailure => 1;

    public virtual string Name => GetType().Name;

    public virtual bool IncludeArgs { get; set; } = true;

    public BooleanVerb(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Task<bool> Execute(T options);

    public virtual void PrintLog(T options)
    {
        if (!IncludeArgs)
        {
            _logger.LogDebug(LOG_MESSAGE, Name);
            return;
        }

        var settings = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        var args = JsonSerializer.Serialize(options, settings);
        _logger.LogDebug(LOG_MESSAGE + "\r\nWith: {args}", Name, args);
    }

    public virtual async Task<int> Run(T options)
    {
        try
        {
            PrintLog(options);
            var result = await Execute(options);
            return result ? ExitCodeSuccess : ExitCodeFailure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while executing: {name}", Name);
            return ExitCodeFailure;
        }
    }
}
