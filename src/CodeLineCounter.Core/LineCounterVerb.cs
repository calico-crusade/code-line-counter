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

    public void Log(LineCounterVerbOptions options, string message, params object[] args)
    {
        if (options.LogOps)
            _logger.LogDebug(message, args);
    }

    public async Task LoadIgnoreFile(string? file, Ignore ignore)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
            return;

        using var io = File.OpenText(file);
        while (!io.EndOfStream)
        {
            var line = await io.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
                continue;

            ignore.Add(line);
        }
    }

    public async Task<Ignore> GetIgnore(LineCounterVerbOptions options)
    {
        var ignore = new Ignore()
            .Add(options.Ignore);

        foreach (var file in options.IgnoreFiles)
            await LoadIgnoreFile(file, ignore);

        if (!options.IncludeLocalIgnores)
            return ignore;

        var ignoreFiles = new[] { ".ignore", ".gitignore", ".npmignore", ".tfignore" };

        var files = ignoreFiles.SelectMany(f => Directory.GetFiles(options.Path!, f, SearchOption.AllDirectories));

        foreach (var file in files)
            await LoadIgnoreFile(file, ignore);

        return ignore;
    }

    public async Task<int> EvaluateFile(string file, LineCounterVerbOptions options)
    {
        Log(options, "Evaluating File: {file}", file);
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
        {
            Log(options, "File does not exist: {file}", file);
            return -1;
        }

        using var io = File.OpenText(file);
        int lines = 0;
        var exludes = new[] { "{", "}", "(", ")" };

        while (!io.EndOfStream)
        {
            var line = await io.ReadLineAsync();
            if (line == null) break;

            if (string.IsNullOrEmpty(line) && !options.IncludeWhiteSpace)
                continue;

            var trimmed = line.Trim();
            if (!options.IncludeBracketLines && exludes.Contains(trimmed))
                continue;

            lines++;
        }

        Log(options, "File has {lines} lines", lines);
        return lines;
    }

    public IEnumerable<string> ErrorsFromCount(int count, string path)
    {
        if (count == -1) yield return $"File does not exist: {path}";
        if (count == -2) yield return $"File was ignored: {path}";
    }

    public IEnumerable<string> GetFiles(string directory, Ignore ignores, string rootDir)
    {
        var ignored = (string path) => 
            ignores.IsIgnored(Path.GetRelativePath(rootDir, path).Replace("\\", "/")) ||
            ignores.IsIgnored(Path.GetRelativePath(directory, path).Replace("\\", "/"));

        if (rootDir != directory && ignored(directory))
            yield break;

        var dirs = Directory.GetDirectories(directory);
        foreach (var dir in dirs)
            foreach (var file in GetFiles(dir, ignores, rootDir))
                yield return file;

        var files = Directory.GetFiles(directory);
        foreach (var file in files)
            if (!ignored(file))
                yield return file;
    }

    public async Task<(int count, string[] errors)> TotalLines(LineCounterVerbOptions options)
    {
        var errors = new List<string>();

        var path = options.Path;
        if (string.IsNullOrEmpty(path))
        {
            errors.Add("No path specified");
            return (0, errors.ToArray());
        }

        if (File.Exists(path))
        {
            var count = await EvaluateFile(path, options);
            errors.AddRange(ErrorsFromCount(count, path));
            return (count < 0 ? 0 : count, errors.ToArray());
        }

        if (!Directory.Exists(path))
        {
            errors.Add($"Could not find file or directory at: {path}");
            return (0, errors.ToArray());
        }

        var ignores = await GetIgnore(options);

        var files = GetFiles(path, ignores, path).ToArray();
        var counts = await Task.WhenAll(files.Select(async (t, i) =>
        {
            var count = await EvaluateFile(t, options);
            var errors = ErrorsFromCount(count, t).ToArray();
            return (count, errors);
        }));

        var total = counts.Sum(t => t.count);
        counts.Each(t => errors.AddRange(t.errors));
        return (total, errors.ToArray());
    }

    public override async Task<bool> Execute(LineCounterVerbOptions options)
    {
        //var (count, errors) = await TotalLines(options);
        //_logger.LogInformation("Finished with {count} lines", count);

        //if (errors.Length == 0)
        //    return true;

        //_logger.LogWarning("There were {count} errors", errors.Length);
        //errors.Each(t => _logger.LogWarning(t));
        //return false;

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