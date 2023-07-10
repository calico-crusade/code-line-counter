namespace CodeLineCounter.Core;

using IOPath = Path;

/// <summary>
/// A bunch of file path methods that are cached for performance.
/// </summary>
public readonly struct CounterPath
{
    /// <summary>
    /// The cache item for the windows file path
    /// </summary>
    private readonly CacheItem<string> _windows;

    /// <summary>
    /// The cache item for the linux file path
    /// </summary>
    private readonly CacheItem<string> _linux;

    /// <summary>
    /// The cache item for the local file path
    /// </summary>
    private readonly CacheItem<string> _local;

    /// <summary>
    /// The cache item for the file name (null if not a file)
    /// </summary>
    private readonly CacheItem<string?> _filename;

    /// <summary>
    /// The cache item for the file extension (null if not a file)
    /// </summary>
    private readonly CacheItem<string?> _extension;

    /// <summary>
    /// The raw path that was passed in.
    /// </summary>
    public readonly string Path { get; }

    /// <summary>
    /// The type of path (file, directory, or not found).
    /// </summary>
    public readonly FileType Type { get; }

    /// <summary>
    /// The path with the correct directory separator for Windows
    /// </summary>
    public readonly string Windows => _windows;

    /// <summary>
    /// The path with the correct directory separator for Linux
    /// </summary>
    public readonly string Linux => _linux;

    /// <summary>
    /// The path with the correct directory separator for the current OS
    /// </summary>
    public readonly string Local => _local;

    /// <summary>
    /// The name of the file (or null if not a file)
    /// </summary>
    public readonly string? FileName => _filename;

    /// <summary>
    /// The extension of the file (or null if not a file)
    /// </summary>
    public readonly string? Extension => _extension;

    /// <summary>
    /// Whether or not the file or directory exists
    /// </summary>
    public readonly bool Exists => Type != FileType.NotFound;

    /// <summary>
    /// A bunch of file path methods that are cached for performance.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <param name="type">The type of path (file, directory or not found)</param>
    public CounterPath(string path, FileType type)
    {
        Path = path;
        Type = type;
        _windows = new(() => FixDirectoryPath(path, type, '\\'));
        _linux = new(() => FixDirectoryPath(path, type, '/'));
        _local = new(() => FixDirectoryPath(path, type));
        _filename = new(GetFileName);
        _extension = new(GetExtension);
    }

    /// <summary>
    /// A bunch of file path methods that are cached for performance.
    /// </summary>
    /// <param name="path">The path to check</param>
    public CounterPath(string path) : this(path, DetermineType(path)) { }

    /// <summary>
    /// Returns the current path relative to the given path
    /// </summary>
    /// <param name="to">The path to use as the relative root</param>
    /// <returns>The relative path</returns>
    public readonly CounterPath Relative(string to)
    {
        return new(IOPath.GetRelativePath(to, Path), Type);
    }

    /// <summary>
    /// Gets the file name for the current path (or null if not a file)
    /// </summary>
    /// <returns></returns>
    private readonly string? GetFileName()
    {
        if (Type != FileType.File)
            return null;

        return IOPath.GetFileName(Path);
    }

    /// <summary>
    /// Gets the file extension for the current path (or null if not a file)
    /// </summary>
    /// <returns></returns>
    private readonly string? GetExtension()
    {
        if (Type != FileType.File)
            return null;

        return IOPath.GetExtension(Path);
    }

    /// <summary>
    /// Returns the current path corrected for the current OS
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Local;

    /// <summary>
    /// Ensures that directories have a trailing slash and files do not, using the given directory separator (or the current OS's separator if none is given)
    /// </summary>
    /// <param name="path">The path to fix</param>
    /// <param name="type">The type of path</param>
    /// <param name="pathChar">The directory separator to use (resolves the current OS's separator if none is given)</param>
    /// <returns>The corrected path</returns>
    public static string FixDirectoryPath(string path, FileType type, char? pathChar = null)
    {
        var pc = pathChar ?? IOPath.DirectorySeparatorChar;
        var parts = path.Split(new [] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        var newPath = string.Join(pc, parts);
        if (type != FileType.Directory)
            return newPath;

        return newPath + pc;
    }

    /// <summary>
    /// Determines the file type for the given path
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>The file type for the given path</returns>
    public static FileType DetermineType(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return FileType.NotFound;

        if (File.Exists(path))
            return FileType.File;

        if (Directory.Exists(path))
            return FileType.Directory;

        return FileType.NotFound;
    }

    /// <summary>
    /// Gets the <see cref="Local"/> path for the given <see cref="CounterPath"/>
    /// </summary>
    /// <param name="path">The counter path to get the <see cref="Local"/> value for</param>
    public static implicit operator string(CounterPath path) => path.Local;

    /// <summary>
    /// Gets an instance of <see cref="CounterPath"/> for the given path
    /// </summary>
    /// <param name="path">The path to get the <see cref="CounterPath"/> for</param>
    public static implicit operator CounterPath(string path) => new(path);
}
