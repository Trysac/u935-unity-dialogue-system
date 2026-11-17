// Adds one NuGet dependency at an exact version. Factory-owned: agents never run it; they list
// dependencies and the factory adds the allowed ones.
//
// Usage: dotnet run --project scripts/AddDependency -- [--test] PackageId [spec]
//        dotnet run --project scripts/AddDependency -- [--test] --git owner repo tag name
//
// spec: empty (the latest stable release on nuget.org), an exact version (1.2.3 or ==1.2.3) or a
// lower bound with an optional upper bound (>=1.2 or >=1.2,<2): the latest stable release that
// satisfies it is written as the exact version (`dotnet add package --version`), and a restore of
// the whole solution updates every packages.lock.json it changes (the tests reference the library). --test adds it to the test project, every
// other dependency goes to the library. Factory repositories (--git) are refused for now: they
// are not published to a NuGet feed.

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fabrica.Scripts;

internal static partial class Program
{
    private const string FlatContainer = "https://api.nuget.org/v3-flatcontainer/";
    private const string LibraryProject = "src/Library.csproj";
    private const string TestProject = "tests/Library.Tests.csproj";
    private const string Solution = "Library.sln";

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}$")]
    private static partial Regex PackageId();

    [GeneratedRegex(@"^[0-9]+(\.[0-9]+){0,3}$")]
    private static partial Regex StableVersion();

    private static async Task<int> Main(string[] args)
    {
        try
        {
            return await Add([.. args]);
        }
        catch (RefusedException e)
        {
            Console.Error.WriteLine($"add-dependency: {e.Message}");
            return 1;
        }
    }

    private static async Task<int> Add(List<string> rest)
    {
        var project = LibraryProject;
        if (rest.Count > 0 && rest[0] == "--test")
        {
            project = TestProject;
            rest.RemoveAt(0);
        }
        if (rest.Count > 0 && rest[0] == "--git")
        {
            throw new RefusedException(
                "factory repositories are not installable in C# yet: they are not published to a NuGet feed");
        }
        if (rest.Count is 0 or > 2 || !PackageId().IsMatch(rest[0]))
        {
            throw new RefusedException("usage: [--test] PackageId [spec]");
        }
        var id = rest[0];
        var version = await Resolve(id, rest.Count == 2 ? rest[1].Trim() : "");
        var dotnet = Environment.ProcessPath is { } host && Path.GetFileNameWithoutExtension(host) == "dotnet"
            ? host
            : "dotnet";
        await Dotnet(dotnet, "add", project, "package", id, "--version", version);
        // The test project references the library, so a library dependency changes its lock file too: restore
        // the whole solution, which rewrites every packages.lock.json that no longer matches its project.
        await Dotnet(dotnet, "restore", Solution);
        Console.WriteLine($"added {id} {version} to {project}");
        return 0;
    }

    private static async Task Dotnet(string dotnet, params string[] args)
    {
        var info = new ProcessStartInfo(dotnet) { UseShellExecute = false };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }
        using var proc = Process.Start(info)!;
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            throw new RefusedException($"`dotnet {string.Join(' ', args)}` failed (exit {proc.ExitCode})");
        }
    }

    /// <summary>The exact version a spec selects; only stable releases are considered.</summary>
    private static async Task<string> Resolve(string id, string spec)
    {
        var exact = spec.StartsWith("==", StringComparison.Ordinal) ? spec[2..].Trim() : spec;
        if (StableVersion().IsMatch(exact))
        {
            return exact;
        }
        Version? lower = null;
        Version? upper = null;
        if (spec.Length > 0)
        {
            foreach (var part in spec.Split(',').Select(p => p.Trim()))
            {
                if (part.StartsWith(">=", StringComparison.Ordinal) && Parse(part[2..].Trim()) is { } low)
                {
                    lower = low;
                }
                else if (part.StartsWith('<') && !part.StartsWith("<=", StringComparison.Ordinal)
                    && Parse(part[1..].Trim()) is { } high)
                {
                    upper = high;
                }
                else
                {
                    throw new RefusedException($"unsupported version spec {spec} (use 1.2.3, >=1.2 or >=1.2,<2)");
                }
            }
        }
        var chosen = (await StableVersions(id))
            .Where(v => (lower is null || v.Parsed >= lower) && (upper is null || v.Parsed < upper))
            .MaxBy(v => v.Parsed);
        return chosen.Text ?? throw new RefusedException($"no stable release of {id} satisfies {spec}");
    }

    private static Version? Parse(string text)
    {
        if (!StableVersion().IsMatch(text))
        {
            return null;
        }
        var parts = text.Split('.').ToList();
        while (parts.Count < 2)
        {
            parts.Add("0");
        }
        return Version.Parse(string.Join('.', parts));
    }

    /// <summary>Every stable version nuget.org lists for the package (pre-releases carry a '-').</summary>
    private static async Task<List<(string? Text, Version Parsed)>> StableVersions(string id)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var url = new Uri($"{FlatContainer}{id.ToLowerInvariant()}/index.json");
        string body;
        try
        {
            body = await http.GetStringAsync(url);
        }
        catch (HttpRequestException e)
        {
            throw new RefusedException($"cannot read the versions of {id} from nuget.org: {e.Message}");
        }
        using var doc = JsonDocument.Parse(body);
        List<(string? Text, Version Parsed)> versions = [];
        if (doc.RootElement.TryGetProperty("versions", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                var text = item.GetString() ?? "";
                if (Parse(text) is { } parsed)
                {
                    versions.Add((text, parsed));
                }
            }
        }
        return versions;
    }
}

/// <summary>A refused or failed addition.</summary>
public sealed class RefusedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public RefusedException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Why the dependency was not added.</param>
    public RefusedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">Why the dependency was not added.</param>
    /// <param name="innerException">The cause.</param>
    public RefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
