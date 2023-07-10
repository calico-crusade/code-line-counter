namespace CodeLineCounter.Verbs.Base;

/// <summary>
/// Represents a verb that returns a boolean result
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class BooleanVerb<T> : IVerb<T> where T : class
{
    /// <summary>
    /// The default message used to indicate execution
    /// </summary>
    public const string LOG_MESSAGE = "Executing: {name}";

    /// <summary>
    /// The logger for the verb
    /// </summary>
    public readonly ILogger _logger;

    /// <summary>
    /// The exit code that indicates success
    /// </summary>
    public virtual int ExitCodeSuccess => 0;

    /// <summary>
    /// The exit code that indicates failure
    /// </summary>
    public virtual int ExitCodeFailure => 1;

    /// <summary>
    /// The name of the verb
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// Whether or not to print the arg options when logging
    /// </summary>
    public virtual bool IncludeArgs { get; set; } = true;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    public BooleanVerb(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the verb
    /// </summary>
    /// <param name="options">The options to execute with</param>
    /// <returns>Whether or not the execution was successful</returns>
    public abstract Task<bool> Execute(T options);

    /// <summary>
    /// Prints out the log message for the verb
    /// </summary>
    /// <param name="options">The options the verb was executed with</param>
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

    /// <summary>
    /// Runs the verb and handles any exceptions
    /// </summary>
    /// <param name="options">The option to execute the verb with</param>
    /// <returns>The exit code the verb finished with (either: <see cref="ExitCodeSuccess"/> or <see cref="ExitCodeFailure"/>)</returns>
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
