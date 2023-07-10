namespace CodeLineCounter.Verbs;

using Base;
using Core;

/// <summary>
/// Represents the options for the line counter verb
/// </summary>
[Verb("lines", true, HelpText = "Counts the number of lines in the given source code")]
public class LineCounterVerbOptions
{
    /// <summary>
    /// The file or directory path to scan
    /// </summary>
    [Option('p', "path", 
        HelpText = "The directory or file to scan", Required = true)]
    public string? Path { get; set; }

    /// <summary>
    /// Any optional ignore rules
    /// </summary>
    [Option('i', "ignore", 
        HelpText = "Optional ignore patterns", Required = false, Separator = ',')]
    public IEnumerable<string> Ignore { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Any ignore files to load rules from
    /// </summary>
    [Option('f', "ignore-files", 
        HelpText = "The files to load ignore patterns from", Required = false)]
    public IEnumerable<string> IgnoreFiles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether or not to count lines that are empty
    /// </summary>
    [Option('w', "white-space", 
        HelpText = "Whether to count white space lines (true) or ignore them (false)", 
        Required = false, Default = false)]
    public bool IncludeWhiteSpace { get; set; }

    /// <summary>
    /// Whether or not to include lines that only have brackets on them
    /// </summary>
    [Option('b', "brackets", 
        HelpText = "Whether to lines that only have brackets on them (true) or ignore them (false)", 
        Required = false, Default = false)]
    public bool IncludeBracketLines { get; set; }

    /// <summary>
    /// Optionally load .gitignore files into the ignore rules from within the given directory
    /// </summary>
    [Option('l', "local-ignores", 
        HelpText = "Whether to load ignore patterns from ignore files in the given path (true) or not (false)",
        Required = false, Default = false)]
    public bool IncludeLocalIgnores { get; set; }

    /// <summary>
    /// Whether or not to log operations to the default logger
    /// </summary>
    [Option('d', "debug-log", 
        HelpText = "Whether to log operations (true) or not (false)", 
        Required = false, Default = true)]
    public bool LogOps { get; set; }
}

/// <summary>
/// The verb used for counting all of the lines in a given directory or file
/// </summary>
public class LineCounterVerb : BooleanVerb<LineCounterVerbOptions>
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    public LineCounterVerb(ILogger<LineCounterVerb> logger) : base(logger) { }

    /// <summary>
    /// Counts all of the lines in the given configuration
    /// </summary>
    /// <param name="options">The configuration options</param>
    /// <returns>Whether or not the operation was successful</returns>
    public override async Task<bool> Execute(LineCounterVerbOptions options)
    {
        if (string.IsNullOrEmpty(options.Path))
        {
            _logger.LogWarning("No path was specified");
            return false;
        }

        var traverser = new FileTraverser(options.Path)
            .WithLogger(options.LogOps ? _logger : null)
            .WithIncludeWhitepsace(options.IncludeWhiteSpace)
            .WithIgnoreRule(options.Ignore.ToArray())
            .WithDefaultRules();

        if (!options.IncludeBracketLines)
            traverser.WithIgnoreBrackets();

        if (options.IncludeLocalIgnores)
            await traverser.WithLocalIgnoreFiles();

        await traverser.WithIgnoreFiles(options.IgnoreFiles.ToArray());

        var total = await traverser.Count();

        foreach (var (ext, count) in traverser.CountsByExtensions)
            _logger.LogInformation("Extension `{ext}` has {count} lines.", ext, count);

        _logger.LogInformation("Total lines: {total}", total);
        return true;
    }
}