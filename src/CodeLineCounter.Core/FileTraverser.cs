using System.Collections.Concurrent;

namespace CodeLineCounter.Core;

using Ignore = Ignore.Ignore;

public interface IFileTraverser
{
    CounterPath InputPath { get; }
    bool IncludeWhiteSpace { get; }
    IReadOnlyList<string> IgnoreRules { get; }
    IReadOnlyList<string> IgnoreCharacters { get; }
    IEnumerable<(string ext, int count)> CountsByExtensions { get; }
    IReadOnlyList<string> FileExtensions { get; }

    IFileTraverser WithLogger(ILogger logger);

    IFileTraverser WithIgnoreCharacters(params string[] characters);

    IFileTraverser WithIgnoreBrackets();

    IFileTraverser WithIncludeWhitepsace(bool include = true);

    IFileTraverser WithFileExtensions(params string[] extensions);

    IFileTraverser WithIgnoreRule(params string[] rules);

    Task WithIgnoreFiles(params string[] files);

    Task WithLocalIgnoreFiles(params string[] patterns);

    Task<int> Count(bool clear = true);
}

public class FileTraverser : IFileTraverser
{
    private readonly ConcurrentBag<FileCount> _counts = new();
    private readonly List<string> _ignoreCharacters = new();
    private readonly List<string> _fileExtensions = new();
    private readonly Ignore _ignores;
    private ILogger? _logger;

    public CounterPath InputPath { get; }
    public bool IncludeWhiteSpace { get; set; } = false;

    public IReadOnlyList<string> IgnoreRules => _ignores.OriginalRules.AsReadOnly();
    public IEnumerable<(string ext, int count)> CountsByExtensions => CountsToDictionary();
    public IReadOnlyList<string> IgnoreCharacters => _ignoreCharacters.AsReadOnly();
    public IReadOnlyList<string> FileExtensions => _fileExtensions.AsReadOnly();

    public FileTraverser(
        string path, 
        Ignore? ignores = null)
    {
        _ignores = ignores ?? new();
        InputPath = path;
    }

    #region Utility Methods
    public bool Ignore(CounterPath path)
    {
        var paths = new[]
        {
            path,
            path.Relative(InputPath)
        }
            .Select(t => t.Linux)
            .Where(t => !string.IsNullOrEmpty(t));

        if (path.Type == FileType.File)
        {
            var dir = Path.GetDirectoryName(path.Local);
            if (!string.IsNullOrEmpty(dir))
                paths = paths.Append(new CounterPath(dir, FileType.Directory).Linux);
        }

        if (paths.Any(_ignores.IsIgnored))
            return true;

        if (_fileExtensions.Count == 0) return false;

        return string.IsNullOrEmpty(path.Extension) || 
            !_fileExtensions.Contains(path.Extension);
    }

    public async Task LoadIgnore(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _logger?.LogWarning("Couldn't find ignore file path: {path}", path);
            return;
        }

        using var io = File.OpenText(path);
        while (!io.EndOfStream)
        {
            var line = await io.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;

            _ignores.Add(line);
        }
    }

    public IEnumerable<CounterPath> GetFiles(string directory, string rootDir, params string[] patterns)
    {
        var resolveFiles = (string path) =>
        {
            if (patterns.Length == 0)
                return Directory.GetFiles(path);

            return patterns.SelectMany(pattern => Directory.GetFiles(path, pattern)).Distinct();
        };

        if (rootDir != directory && Ignore(directory))
            yield break;

        var files = resolveFiles(directory);
        foreach (var file in files)
            if (!Ignore(file))
                yield return new CounterPath(file, FileType.File);

        var dirs = Directory.GetDirectories(directory);
        foreach (var dir in dirs)
            foreach (var file in GetFiles(dir, rootDir, patterns))
                yield return file;
    }

    public IEnumerable<(string ext, int count)> CountsToDictionary()
    {
        return _counts
            .GroupBy(t => t.Path.Extension ?? "")
            .Select(t => (t.Key, t.Sum(x => x.Count)))
            .OrderByDescending(t => t.Item2);
    }
    #endregion

    #region Builder Methods
    public IFileTraverser WithLogger(ILogger? logger)
    {
        _logger = logger;
        return this;
    }

    public IFileTraverser WithIgnoreCharacters(params string[] characters)
    {
        _ignoreCharacters.AddRange(characters);
        return this;
    }

    public IFileTraverser WithIgnoreBrackets() => WithIgnoreCharacters("{", "}", "[", "]", "(", ")");

    public IFileTraverser WithIncludeWhitepsace(bool include = true)
    {
        IncludeWhiteSpace = include;
        return this;
    }

    public IFileTraverser WithFileExtensions(params string[] extensions)
    {
        _fileExtensions.AddRange(extensions);
        return this;
    }

    public IFileTraverser WithIgnoreRule(params string[] rules)
    {
        _ignores.Add(rules);
        return this;
    }

    public Task WithIgnoreFiles(params string[] files)
    {
        return Task.WhenAll(files.Select(LoadIgnore));
    }

    public async Task WithLocalIgnoreFiles(params string[] patterns)
    {
        var filePatterns = patterns.Length == 0 ? new[] { "*.ignore", "*.gitignore" } : patterns;

        var files = GetFiles(InputPath, InputPath, filePatterns);
        foreach (var file in files)
        {
            _logger?.LogDebug("Loading ignore file: {file}", file);
            await LoadIgnore(file);
        }
    }
    #endregion

    public async Task<int> EvaluateFile(string file)
    {
        _logger?.LogDebug("Evaluating file: {file}", file);
        if (string.IsNullOrEmpty(file))
        {
            _logger?.LogWarning("Couldn't find file: {file}", file);
            return 0;
        }

        using var io = File.OpenText(file);
        int lines = 0;

        while (!io.EndOfStream)
        {
            var line = await io.ReadLineAsync();
            if (line == null) break;

            line = line.Trim();
            if (string.IsNullOrEmpty(line) && !IncludeWhiteSpace)
                continue;

            if (_ignoreCharacters.Contains(line))
                continue;

            lines++;
        }

        _logger?.LogDebug("File has {lines} lines: {file}", lines, file);
        return lines;
    }

    public async Task<int> Count(bool clear = true)
    {
        if (clear) _counts.Clear();
        if (!InputPath.Exists) return -1;

        if (InputPath.Type == FileType.File)
        {
            var count = await EvaluateFile(InputPath.Local);
            _counts.Add(new(InputPath, count));
            return count;
        }

        var files = GetFiles(InputPath, InputPath);

        _logger?.LogDebug("Starting file evaluations with {rules} rules", IgnoreRules.Count);
        await Task.WhenAll(files.Select(async t =>
        {
            var count = await EvaluateFile(t.Local);
            _counts.Add(new(t, count));
        }));
        var total = _counts.Sum(t => t.Count);
        _logger?.LogDebug("Finished file evaluations with {total} lines", total);
        return total;
    }
}

public record class FileCount(CounterPath Path, int Count);