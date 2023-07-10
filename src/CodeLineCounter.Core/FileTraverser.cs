using System.Collections.Concurrent;

namespace CodeLineCounter.Core;

using Ignore = Ignore.Ignore;

/// <summary>
/// A helper class used for counting the lines in source code files
/// </summary>
public interface IFileTraverser
{
    /// <summary>
    /// The path to the file or directory to count
    /// </summary>
    CounterPath InputPath { get; }

    /// <summary>
    /// Whether or not to count lines that just have white space
    /// </summary>
    bool IncludeWhiteSpace { get; }

    /// <summary>
    /// The rules used to determine whether or not to ignore a file or directory
    /// </summary>
    IReadOnlyList<string> IgnoreRules { get; }

    /// <summary>
    /// Characters to ignore when counting lines
    /// </summary>
    IReadOnlyList<string> IgnoreCharacters { get; }

    /// <summary>
    /// The counts of lines by extension
    /// </summary>
    IEnumerable<(string ext, int count)> CountsByExtensions { get; }

    /// <summary>
    /// A list of file extensions to count. 
    /// </summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Uses the given logger for logging
    /// </summary>
    /// <param name="logger">The logger to use</param>
    /// <returns>The current traverser for method chaining</returns>
    IFileTraverser WithLogger(ILogger logger);

    /// <summary>
    /// Adds the given characters to the <see cref="IgnoreCharacters"/>
    /// </summary>
    /// <param name="characters">The characters to ignore</param>
    /// <returns>The current traverser for method chaining</returns>
    IFileTraverser WithIgnoreCharacters(params string[] characters);

    /// <summary>
    /// Ignores any brackets, parenthesis, or square brackets
    /// </summary>
    /// <returns>The current traverser for method chaining</returns>
    IFileTraverser WithIgnoreBrackets();

    /// <summary>
    /// Whether or not to include whitespace in the line count
    /// </summary>
    /// <param name="include">Whether or not to include whitespace in the line count (default: true)</param>
    /// <returns>The current traverser for method chaining</returns>
    IFileTraverser WithIncludeWhitepsace(bool include = true);

    /// <summary>
    /// Adds the given file extensions to the <see cref="FileExtensions"/>
    /// </summary>
    /// <param name="extensions">The file extensions to include</param>
    /// <returns>The current traverser for method chaining</returns>
    IFileTraverser WithFileExtensions(params string[] extensions);

    /// <summary>
    /// Adds the given ignore rules to the <see cref="IgnoreRules"/>
    /// </summary>
    /// <param name="rules">The ignore rules</param>
    /// <returns>The current traverser for method chaining</returns>
    IFileTraverser WithIgnoreRule(params string[] rules);

    /// <summary>
    /// Adds some default rules to exclude images, fonts, and some common directories
    /// </summary>
    /// <returns></returns>
    IFileTraverser WithDefaultRules();

    /// <summary>
    /// Loads the given ignore file paths
    /// </summary>
    /// <param name="files">The ignore file paths to load</param>
    /// <returns></returns>
    Task WithIgnoreFiles(params string[] files);

    /// <summary>
    /// Loads all of the ignore files in the given directory (and subdirectories) that match the given patterns (or .ignore and .gitignore by default)
    /// </summary>
    /// <param name="patterns">The ignore file patterns</param>
    /// <returns></returns>
    Task WithLocalIgnoreFiles(params string[] patterns);

    /// <summary>
    /// Counts all of the lines of all of the files in the given directory (and subdirectories)
    /// </summary>
    /// <param name="clear">Whether or not to clear the counts list (useful for subsequent runs)</param>
    /// <returns>The total number of lines</returns>
    Task<int> Count(bool clear = true);
}

/// <summary>
/// A helper class used for counting the lines in source code files
/// </summary>
public class FileTraverser : IFileTraverser
{
    private readonly ConcurrentBag<FileCount> _counts = new();
    private readonly List<string> _ignoreCharacters = new();
    private readonly List<string> _fileExtensions = new();
    private readonly Ignore _ignores;
    private ILogger? _logger;

    /// <summary>
    /// The path to the file or directory to count
    /// </summary>
    public CounterPath InputPath { get; }

    /// <summary>
    /// Whether or not to count lines that just have white space
    /// </summary>
    public bool IncludeWhiteSpace { get; set; } = false;

    /// <summary>
    /// The rules used to determine whether or not to ignore a file or directory
    /// </summary>
    public IReadOnlyList<string> IgnoreRules => _ignores.OriginalRules.AsReadOnly();

    /// <summary>
    /// The counts of lines by extension
    /// </summary>
    public IEnumerable<(string ext, int count)> CountsByExtensions => CountsByExtension();

    /// <summary>
    /// Characters to ignore when counting lines
    /// </summary>
    public IReadOnlyList<string> IgnoreCharacters => _ignoreCharacters.AsReadOnly();

    /// <summary>
    /// A list of file extensions to count. 
    /// </summary>
    public IReadOnlyList<string> FileExtensions => _fileExtensions.AsReadOnly();

    /// <summary>
    /// A helper class used for counting the lines in source code files
    /// </summary>
    /// <param name="path">The file or directory path</param>
    /// <param name="ignores">The starting ignore rules</param>
    public FileTraverser(
        string path, 
        Ignore? ignores = null)
    {
        _ignores = ignores ?? new();
        InputPath = path;
    }

    #region Utility Methods

    /// <summary>
    /// Determines whether or not the given path should be ignored
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>Whether or not the path should be ignored</returns>
    public bool Ignore(CounterPath path)
    {
        var paths = new[]
        {
            path,
            path.Relative(InputPath)
        }
            .Select(t => t.Unix)
            .Where(t => !string.IsNullOrEmpty(t));

        if (path.Type == FileType.File)
        {
            var dir = Path.GetDirectoryName(path.Local);
            if (!string.IsNullOrEmpty(dir))
                paths = paths.Append(new CounterPath(dir, FileType.Directory).Unix);
        }

        if (paths.Any(_ignores.IsIgnored))
            return true;

        if (_fileExtensions.Count == 0) return false;

        return string.IsNullOrEmpty(path.Extension) || 
            !_fileExtensions.Contains(path.Extension);
    }

    /// <summary>
    /// Loads the given ignore file rules
    /// </summary>
    /// <param name="path">The path to the ignore file</param>
    /// <returns></returns>
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

    /// <summary>
    /// Returns all of the files in the given directory (and subdirectories) that pass that shouldn't be ignored
    /// </summary>
    /// <param name="directory">The directory to search</param>
    /// <param name="rootDir">The root directory to start from</param>
    /// <param name="patterns">Optional file-name patterns (if empty, all files will be returned)</param>
    /// <returns>The path of all of the files that were not ignored</returns>
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

    /// <summary>
    /// Gets the line counts by extensions
    /// </summary>
    /// <returns></returns>
    public IEnumerable<(string ext, int count)> CountsByExtension()
    {
        return _counts
            .GroupBy(t => t.Path.Extension ?? "")
            .Select(t => (t.Key, t.Sum(x => x.Count)))
            .OrderByDescending(t => t.Item2);
    }

    #endregion

    #region Builder Methods
    /// <summary>
    /// Uses the given logger for logging
    /// </summary>
    /// <param name="logger">The logger to use</param>
    /// <returns>The current traverser for method chaining</returns>
    public IFileTraverser WithLogger(ILogger? logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Adds the given characters to the <see cref="IgnoreCharacters"/>
    /// </summary>
    /// <param name="characters">The characters to ignore</param>
    /// <returns>The current traverser for method chaining</returns>
    public IFileTraverser WithIgnoreCharacters(params string[] characters)
    {
        _ignoreCharacters.AddRange(characters);
        return this;
    }

    /// <summary>
    /// Ignores any brackets, parenthesis, or square brackets
    /// </summary>
    /// <returns>The current traverser for method chaining</returns>
    public IFileTraverser WithIgnoreBrackets() => WithIgnoreCharacters("{", "}", "[", "]", "(", ")");

    /// <summary>
    /// Whether or not to include whitespace in the line count
    /// </summary>
    /// <param name="include">Whether or not to include whitespace in the line count (default: true)</param>
    /// <returns>The current traverser for method chaining</returns>
    public IFileTraverser WithIncludeWhitepsace(bool include = true)
    {
        IncludeWhiteSpace = include;
        return this;
    }

    /// <summary>
    /// Adds the given file extensions to the <see cref="FileExtensions"/>
    /// </summary>
    /// <param name="extensions">The file extensions to include</param>
    /// <returns>The current traverser for method chaining</returns>
    public IFileTraverser WithFileExtensions(params string[] extensions)
    {
        _fileExtensions.AddRange(extensions);
        return this;
    }

    /// <summary>
    /// Adds the given ignore rules to the <see cref="IgnoreRules"/>
    /// </summary>
    /// <param name="rules">The ignore rules</param>
    /// <returns>The current traverser for method chaining</returns>
    public IFileTraverser WithIgnoreRule(params string[] rules)
    {
        _ignores.Add(rules);
        return this;
    }

    /// <summary>
    /// Adds some default rules to exclude images, fonts, and some common directories
    /// </summary>
    /// <returns></returns>
    public IFileTraverser WithDefaultRules()
    {
        return WithIgnoreRule(".git/", ".angular/", ".github/", "*.gif", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.ico", "*.ttf", "*.data");
    }

    /// <summary>
    /// Loads the given ignore file paths
    /// </summary>
    /// <param name="files">The ignore file paths to load</param>
    /// <returns></returns>
    public Task WithIgnoreFiles(params string[] files)
    {
        return Task.WhenAll(files.Select(LoadIgnore));
    }

    /// <summary>
    /// Loads all of the ignore files in the given directory (and subdirectories) that match the given patterns (or .ignore and .gitignore by default)
    /// </summary>
    /// <param name="patterns">The ignore file patterns</param>
    /// <returns></returns>
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

    /// <summary>
    /// Evaluates the given file and returns the number of lines
    /// </summary>
    /// <param name="file">The path to the file to evaluate</param>
    /// <returns>The number of lines in the file</returns>
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

    /// <summary>
    /// Counts all of the lines of all of the files in the given directory (and subdirectories)
    /// </summary>
    /// <param name="clear">Whether or not to clear the counts list (useful for subsequent runs)</param>
    /// <returns>The total number of lines</returns>
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