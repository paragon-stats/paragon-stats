using System.Diagnostics;

namespace ParagonStats.Core.Tests;

/// <summary>
/// Runs the Husky hooks and the file-based scripts in <c>scripts/dev</c> the
/// way git and a developer's shell run them, against scratch repositories.
/// </summary>
internal static class DevScripts
{
    internal static readonly string RepositoryRoot = FindRoot(AppContext.BaseDirectory);

    /// <summary>
    /// Gets the sh git runs hooks with. On Windows that is Git for Windows' own,
    /// found from git's exec path (<c>&lt;Git&gt;/mingw64/libexec/git-core</c>).
    /// </summary>
    internal static string Shell => OperatingSystem.IsWindows()
        ? Path.GetFullPath(Path.Join(Run(RepositoryRoot, "git", input: null, environment: null, "--exec-path").Output.Trim(), "..", "..", "..", "usr", "bin", "sh.exe"))
        : "sh";

    internal static (int Exit, string Output, string Error) Run(string directory, string program, string? input, IReadOnlyDictionary<string, string>? environment, params string[] arguments)
    {
        ProcessStartInfo info = new(program)
        {
            WorkingDirectory = directory,
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // A hook that runs the tests points these at its own repository; the working directory must decide.
        foreach (string name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "SUPER_LINTER_LOG_LEVEL" })
        {
            info.Environment.Remove(name);
        }

        // The coverage collector and the Sonar scanner hook child builds too; the scripts are not the code under test.
        info.Environment["CORECLR_ENABLE_PROFILING"] = "0";
        info.Environment["SonarQubeExclude"] = "true";

        // The developer's own git config must not change what git does here (core.autocrlf with core.safecrlf refuses an LF file).
        info.Environment["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        foreach ((string name, string value) in environment ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            info.Environment[name] = value;
        }

        using Process process = Process.Start(info) ?? throw new InvalidOperationException($"could not start {program}");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (input is not null)
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }

        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    /// <summary>Runs <c>dotnet run scripts/dev/&lt;script&gt; -- args</c> from a directory, under the repository's pinned SDK.</summary>
    internal static (int Exit, string Output, string Error) RunScript(string script, string directory, IReadOnlyDictionary<string, string>? environment, params string[] arguments)
    {
        string pin = Path.Join(directory, "global.json");
        if (!File.Exists(pin))
        {
            File.Copy(Path.Join(RepositoryRoot, "global.json"), pin);
        }

        (int exit, string output, string error) = Run(directory, "dotnet", input: null, environment, ["run", Path.Join(RepositoryRoot, "scripts", "dev", script), "--", .. arguments]);

        // A script that does not compile exits 1 too, which must not read as the check's verdict.
        Assert.DoesNotContain("The build failed", output + error, StringComparison.Ordinal);
        return (exit, output, error);
    }

    /// <summary>Runs git, fails the test when git fails, and returns what git printed.</summary>
    internal static string Git(string directory, params string[] arguments)
    {
        (int exit, string output, string error) = Run(directory, "git", input: null, environment: null, arguments);
        Assert.True(exit == 0, $"git {string.Join(' ', arguments)}: {output}{error}");
        return output.Trim();
    }

    /// <summary>Makes a commit of the index without signing or a configured identity, as scratch repositories have neither.</summary>
    internal static void Commit(string directory) =>
        Git(directory, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--quiet", "-m", "scratch");

    /// <summary>Makes an empty git repository at a new folder under <paramref name="parent"/>.</summary>
    internal static string NewRepository(string parent)
    {
        string repository = Directory.CreateDirectory(Path.Join(parent, "repo")).FullName;
        Git(repository, "init", "--quiet");
        return repository;
    }

    /// <summary>Writes a file with LF line endings, creating its folders.</summary>
    internal static void Write(string repository, string path, string text)
    {
        string full = Path.Join(repository, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    /// <summary>Copies a file from this repository into a scratch one at the same path.</summary>
    internal static void Copy(string repository, string path)
    {
        string target = Path.Join(repository, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Join(RepositoryRoot, path), target, overwrite: true);
    }

    /// <summary>Deletes a scratch tree; git writes its objects read-only, which a plain recursive delete refuses on Windows.</summary>
    internal static void Delete(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }

    private static string FindRoot(string start)
    {
        for (DirectoryInfo? folder = new(start); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Join(folder.FullName, "ParagonStats.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"no ParagonStats.slnx above {start}");
    }
}
