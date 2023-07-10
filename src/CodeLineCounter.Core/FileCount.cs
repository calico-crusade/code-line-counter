namespace CodeLineCounter.Core;

/// <summary>
/// Represents how many lines were counted in a file
/// </summary>
/// <param name="Path">The file path</param>
/// <param name="Count">The number of lines counted</param>
public record class FileCount(CounterPath Path, int Count);
