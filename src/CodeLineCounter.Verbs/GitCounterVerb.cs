using System.Diagnostics;
using System.Net;

namespace CodeLineCounter.Verbs;

using Base;
using Core;

/// <summary>
/// Represents the options for the git line counter verb
/// </summary>
[Verb("git", false, HelpText = "Counts the number of lines in the given git repo")]
public class GitCounterVerbOptions
{
    /// <summary>
    /// The optional URL of the git repo (If non is provided owner and repo are required)
    /// </summary>
    [Option('u', "url", HelpText = "The url of the git repo", Required = false)]
    public string? Url { get; set; }

    /// <summary>
    /// The domain of the git repo (defaults to github.com)
    /// </summary>
    [Option('h', "host", HelpText = "The git repo host to use", Required = false, Default = "github.com")]
    public string? Host { get; set; }

    /// <summary>
    /// The username of the owner or the organization of the repo
    /// </summary>
    [Option('o', "owner", HelpText = "The owner of the repo", Required = false)]
    public string? Owner { get; set; }

    /// <summary>
    /// The name of the repo
    /// </summary>
    [Option('r', "repo", HelpText = "The name of the repo", Required = false)]
    public string? Repo { get; set; }

    /// <summary>
    /// The username to use for authentication
    /// </summary>
    [Option('n', "username", HelpText = "The username to use for authentication", Required = false)]
    public string? Username { get; set; }

    /// <summary>
    /// The password to use for authentication
    /// </summary>
    [Option('p', "password", HelpText = "The password to use for authentication", Required = false)]
    public string? Password { get; set; }

    /// <summary>
    /// Whether to use HTTP (true) or HTTPS (false) for the request
    /// </summary>
    [Option('t', "http", HelpText = "Whether or not to use HTTP for the request", Default = false, Required = false)]
    public bool Http { get; set; }

    /// <summary>
    /// Whether or not to format the URL to use SSH instead of HTTPS
    /// </summary>
    [Option('s', "use-ssh", HelpText = "Whether or not to use SSH for the request", Default = false, Required = false)]
    public bool Ssh { get; set; }

    /// <summary>
    /// The optional working directory to use for the git clone command
    /// </summary>
    [Option('w', "working-directory", HelpText = "The working directory to use for the git command", Required = false)]
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Whether to delete the working directory before and after the git command is run
    /// </summary>
    [Option('v', "preserve-working-directory", HelpText = "Whether or not to preserve the working directory before running the git command", Required = false, Default = false)]
    public bool PreserveWorkingDirectory { get; set; }
}

/// <summary>
/// The verb used to count the number of lines in a git repo
/// </summary>
public class GitCounterVerb : BooleanVerb<GitCounterVerbOptions>
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    public GitCounterVerb(ILogger<GitCounterVerb> logger) : base(logger) { }

    /// <summary>
    /// Formats the given values as a valid git SSH URL
    /// </summary>
    /// <param name="host">The domain of the URL (ex: github.com)</param>
    /// <param name="owner">The organization or owner of the repo (ex: calico-crusade)</param>
    /// <param name="repo">The name of the repo (ex: code-line-counter)</param>
    /// <returns>The properly formatted SSH git URL (ex: git@github.com/calico-crusade/code-line-counter.git)</returns>
    public static string? SshUrl(string? host, string? owner, string? repo)
    {
        if (string.IsNullOrEmpty(owner) ||
            string.IsNullOrEmpty(repo))
            return null;

        return $"git@{host}:{owner}/{repo}.git";
    }

    /// <summary>
    /// Formats the given values as a valid git HTTP(S) URL
    /// </summary>
    /// <param name="host">The domain of the URL (ex: github.com)</param>
    /// <param name="owner">The organization or owner of the repo (ex: calico-crusade)</param>
    /// <param name="repo">The name of the repo (ex: code-line-counter)</param>
    /// <param name="username">The username to use for authentication (ex: calico-crusade)</param>
    /// <param name="password">The password to use for authentication (ex: SuperSecretP4ssw0rd)</param>
    /// <param name="http">Whether to use HTTP (true) or HTTPS (false)</param>
    /// <returns>The properly formatted HTTP(S) git URL (ex: https://calico-crusade:SuperSecretP4ssw0rd@github.com/calico-crusade/code-line-counter)</returns>
    public static string? HttpsUrl(string? host, string? owner, string? repo, string? username, string? password, bool http)
    {
        if (string.IsNullOrEmpty(owner) || 
            string.IsNullOrEmpty(repo))
            return null;

        string domain = $"{host}/{owner}/{repo}";
        string schema = http ? "http" : "https";

        if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(username))
            return $"{schema}://{domain}";

        username = WebUtility.UrlEncode(username);
        if (string.IsNullOrEmpty(password))
            return $"{schema}://{username}@{domain}";

        password = WebUtility.UrlEncode(password);
        return $"{schema}://{username}:{password}@{domain}";
    }

    /// <summary>
    /// Gets the valid git URL from the given options
    /// </summary>
    /// <param name="options">The options used to build the URL</param>
    /// <returns>The properly formatted git URL</returns>
    public static string? UrlFromOptions(GitCounterVerbOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Url))
            return options.Url;

        var url = options.Ssh ?
            SshUrl(options.Host, options.Owner, options.Repo) :
            HttpsUrl(options.Host, options.Owner, options.Repo, 
                options.Username, options.Password, options.Http);

        if (string.IsNullOrEmpty(url))
            return null;

        return url;
    }

    /// <summary>
    /// Cleans the given directory of all files and optionally deletes the directory
    /// </summary>
    /// <param name="dir">The directory to clean</param>
    /// <param name="deleteDir">Whether or not to delete the directory after cleaning (defaults to false)</param>
    /// <returns>Whether or not the operation completed without any errors</returns>
    public bool CleanDirectory(string dir, bool deleteDir = false)
    {
        var directory = new DirectoryInfo(dir);

        directory
            .GetFileSystemInfos("*", SearchOption.AllDirectories)
            .Each(t => t.Attributes = FileAttributes.Normal);

        var failures = directory
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(file =>
            {
                try
                {
                    file.Delete();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't clean up file: {file}", file.FullName);
                    return false;
                }
            })
            .Count(t => !t);

        if (failures == 0 && deleteDir)
            directory.Delete(true);

        return failures == 0;
    }

    /// <summary>
    /// Triggers the git clone command with the given URL and directory
    /// </summary>
    /// <param name="url">The URL to the git repo</param>
    /// <param name="directory">The directory to clone the repo to</param>
    /// <returns>Whether or not the operation was a success</returns>
    public async Task<bool> Clone(string url, string directory)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {url}",
                    WorkingDirectory = directory,
                }
            };

            proc.Start();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while cloning working git repo: {url}", url);
            return false;
        }
    }

    /// <summary>
    /// Clones and counts all of the lines from the git repo specified in the options
    /// </summary>
    /// <param name="options">The options for the request</param>
    /// <returns>Whether or not the operation was a success</returns>
    public override async Task<bool> Execute(GitCounterVerbOptions options)
    {
        var url = UrlFromOptions(options);
        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("No url was provided");
            return false;
        }

        var wkDir = options.WorkingDirectory ?? Directory.CreateTempSubdirectory().FullName;
        _logger.LogDebug("Working Directory: {directory}", wkDir);
        var exists = Directory.Exists(wkDir);

        if (exists && !options.PreserveWorkingDirectory)
        {
            _logger.LogDebug("Deleting working directory (for cleanup)");
            
        }

        if (!exists)
        {
            _logger.LogDebug("Creating working directory: {directory}", wkDir);
            Directory.CreateDirectory(wkDir);
        }

        if (!await Clone(url, wkDir))
            return false;

        var traverser = new FileTraverser(wkDir)
            .WithLogger(_logger)
            .WithIncludeWhitepsace(false)
            .WithIgnoreBrackets()
            .WithDefaultRules();

        await traverser.WithLocalIgnoreFiles();

        var total = await traverser.Count();

        foreach (var (ext, count) in traverser.CountsByExtensions)
            _logger.LogInformation("Extension `{ext}` has {count} lines.", ext, count);

        _logger.LogInformation("Total lines: {total}", total);

        if (options.PreserveWorkingDirectory) return true;

        _logger.LogDebug("Deleting working directory (for cleanup)");

        if (!CleanDirectory(wkDir, true))
        {
            _logger.LogError("Couldn't clean working directory");
            return false;
        }

        return true;
    }
}
