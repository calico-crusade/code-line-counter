namespace CodeLineCounter.Core;

/// <summary>
/// Represents the type of path (file, directory, or not found)
/// </summary>
public enum FileType
{
    /// <summary>
    /// The path was not found
    /// </summary>
    NotFound = 0,
    /// <summary>
    /// The path resolved to a file
    /// </summary>
    File = 1,
    /// <summary>
    /// The path resolved to a directory
    /// </summary>
    Directory = 2
}
