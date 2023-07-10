# code-line-counter
Just a simple command line tool for counting how many lines of code are in a file, directory, or github repo.

Mostly written because I was bored one afternoon.

## Installation
Build with Visual Studio or the dotnet cli:
```
dotnet build
```

CD into the CLI's bin directory and run the exec with any valid verb and options.
```
cd ./CodeLineCounter.Cli/bin/Release/net7.0
```

## Usage
There are 2 different verbs (for now), one for a local directory/file or one for a git repo.

### Local Directory/File
The verb to use is `lines`. The valid options are:

* `-p` or `--path` (Required) - The path to the file or directory to count lines for.
* `-i` or `--ignore` (Optional) - A comma separated list of .gitignore style rules.
* `-f` or `--ignore-files` (Optional) - A comma separated list of files containing .gitignore style rules.
* `-l` or `--local-ignores` (Optional) - A boolean flag indicating whether (`true`) or not (`false`) to use the .gitignore or .ignore files in the directory specified by the `--path` option. (Defaults to `false`).
* `-w` or `--white-space` (Optional) - A boolean flag indicating whether (`true`) or not (`false`) to count white space lines (Defaults to `false`).
* `-b` or `--brackets` (Optional) - A boolean flag indicating whether (`true`) or not (`false`) to count lines containing only brackets, parantheses, or braces (Defaults to `false`).
* `-d` or `--debug-log` (Optional) A boolean flag indicating whether (`true`) or not (`false`) to log the traversal operations to the console (Defaults to `true`).

Example usage:
```
CodeLineCounter.cli.exe lines -p "./../some-code-repo" -l true
```

### Git Repo
The verb to use is `git`. This assumes you have the git-bash command line tool available in your paths files. The valid options are:

(All examples assume you're counting the lines in this current git repo!)
* `-u` or `--url` (Optional) - The url to the git repo to count lines for (ex: `https://github.com/calico-crusade/code-line-counter`)
* `-o` or `--owner` (Optional) - The organization or user that owns the repo to count lines for (ex: `calico-crusade`)
* `-r` or `--repo` (Optional) - The name of the repo to count lines for (ex: `code-line-counter`)
* `-n` or `--username` (Optional) - The optional username to use for authentication when cloning the git repo (only works with non-ssh URLs)
* `-p` or `--password` (Optional) - The optional password to use for authentication when cloning the git repo (only works with non-ssh URLs)
* `-s` or `--use-ssh` (Optional) - Uses the SSH version of the git URL (Defaults to `false`)
* `-w` or `--working-directory` (Optional) - The working directory to use for the clone command. (Defaults to the users temp directory)
* `-v` or `--preserve-working-directory` (Optional) - A boolean flag indicating whether (`true`) or not (`false`) to clean up the working directory after the command has finished. (Defaults to `false`)

Note: You either specify the `--url` option or the `--owner` and `--repo` options, if both are specified, the `--url` option takes precedence. 
If neither are specified an error will be thrown.

Example usage:
```
CodeLineCounter.cli.exe git -o calico-crusade -r code-line-counter
```

## Usage in other projects
While I don't provide a nuget package for this, it can be used in other projects. 
You'll mostly want the `CodeLineCounter.Core` project, which contains the `FileTraverser` class.
An example of how to use it is present in both of the verb classes in the `CodeLineCounter.Verbs` project, but here is a general overview:

```csharp
using CodeLineCounter.Core;

var dir = "./some-directory";

var traverser = new FileTraverser(dir)
	//Provide the logger to use for logging the traversal operations (uses default ILoggers from Microsoft.Extensions.Logging)
	.WithLogger(_logger)
	//Whether or not to include whitespace lines in the count
	.WithIncludeWhitespace(true)
	//Whether or not to ignore lines that only contain brackets, parantheses, or braces 
	.WithIgnoreBrackets()
	//Include some default rules that exclude images, .git and .github directories
	.WithDefaultRules();

//Load any .gitignore or .ignore files in the directory specified
await traverser.WithLocalIgnoreFiles();
//Load the ignore rules from the specified files
await traverser.WithIgnoreFiles("./some-other-directory/.gitignore", "./some-other-place/.ignore");

//Count all of the lines in the files and return the total
var total = await traverser.Count();

//Display the total lines for each file extension
foreach (var (ext, count) in traverser.CountsByExtensions)
    _logger.LogInformation("Extension `{ext}` has {count} lines.", ext, count);
```
