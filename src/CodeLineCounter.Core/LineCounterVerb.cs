namespace CodeLineCounter.Core;

using Ignore = Ignore.Ignore;

using Base;

[Verb("lines", true, HelpText = "Counts the number of lines in the given source code")]
public class LineCounterVerbOptions
{
    [Option('p', "path", HelpText = "The directory or file to scan", Required = true)]
    public string? Path { get; set; }

    [Option('i', "ignore", HelpText = "Optional ignore patterns", Required = false, Separator = ',')]
    public IEnumerable<string> Ignore { get; set; } = Array.Empty<string>();

    [Option('f', "ignore-files", HelpText = "The files to load ignore patterns from", Required = false)]
    public IEnumerable<string> IgnoreFiles { get; set; } = Array.Empty<string>();

    [Option('w', "white-space", HelpText = "Whether to count white space lines (true) or ignore them (false)", Required = false, Default = false)]
    public bool IncludeWhiteSpace { get; set; }

    [Option('b', "brackets", HelpText = "Whether to lines that only have brackets on them (true) or ignore them (false)", Required = false, Default = false)]
    public bool IncludeBracketLines { get; set; }

    [Option('l', "local-ignores", HelpText = "Whether to load ignore patterns from ignore files in the given path (true) or not (false)", Required = false, Default = false)]
    public bool IncludeLocalIgnores { get; set; }

    [Option('d', "debug-log", HelpText = "Whether to log operations (true) or not (false)", Required = false, Default = true)]
    public bool LogOps { get; set; }
}

public class LineCounterVerb : BooleanVerb<LineCounterVerbOptions>
{
    public LineCounterVerb(ILogger<LineCounterVerb> logger) : base(logger) { }

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
            .WithIgnoreRule(options.Ignore.ToArray());

        if (!options.IncludeBracketLines)
            traverser.WithIgnoreBrackets();

        if (options.IncludeLocalIgnores)
            await traverser.WithLocalIgnoreFiles();

        await traverser.WithIgnoreFiles(options.IgnoreFiles.ToArray());

        var total = await traverser.Count();

        foreach(var (ext, count) in traverser.CountsByExtensions)
            _logger.LogInformation("Extension `{ext}` has {count} lines.", ext, count);

        _logger.LogInformation("Total lines: {total}", total);
        return true;
    }
}