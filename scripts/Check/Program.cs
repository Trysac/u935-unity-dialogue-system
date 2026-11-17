// Runs every quality gate of this repository, in order, stopping at the first failure.
//
// Usage: `dotnet run --project scripts/Check --` (the same command locally, in the factory and in
// CI); append gate names to run only those, in the standard order.
//
// Gates: dotnet format (whitespace, style and analyzers) after a locked-mode restore, the build with
// the .NET analyzers (latest-recommended) and nullable warnings as errors, semgrep (Semgrep CE with
// the factory's own rules in .semgrep/), tests (xUnit and FsCheck under coverlet) with
// line coverage >= 80 % of the library, documentation manifest, secrets (gitleaks), known
// vulnerabilities of every resolved NuGet package (dotnet list package --vulnerable) and
// OSV-Scanner over each packages.lock.json (high severity). No shell: it behaves the same on
// Windows and Linux. Factory-owned file: agents must not edit it.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace Fabrica.Scripts;

internal static class Program
{
    private const double MinCoverage = 80.0;
    private const string Solution = "Library.sln";
    private const string DocsManifest = "docs-manifest.json";
    private const string AllowMissingGitleaks = "FABRICA_ALLOW_MISSING_GITLEAKS";
    private const string AllowOfflineAudit = "FABRICA_ALLOW_OFFLINE_AUDIT";
    // FABRICA_CI=1 also runs the tests with the trait Category=conformance (external tools or
    // downloads); only the CI conformance job sets it, so local gates and coverage skip them.
    private const string CiTier = "FABRICA_CI";
    private const string LocalTierFilter = "Category!=conformance";
    // The factory's own Semgrep rules; Semgrep-maintained rules are never used (license).
    private const string SemgrepRules = ".semgrep/factory-rules.yml";
    private const string LockFile = "packages.lock.json";
    private const double HighCvss = 7.0;
    // osv-scanner's exit codes: 1 when vulnerabilities were found, 128 when no package was found.
    private const int OsvFound = 1;
    private const int OsvNoPackages = 128;
    private static readonly string[] Gates = ["format", "lint", "semgrep", "tests", "docs", "secrets", "audit", "osv"];

    private static int Main(string[] args)
    {
        if (!File.Exists(Solution) || !File.Exists("scripts/Check/Program.cs"))
        {
            Console.Error.WriteLine($"check: run from the repository root ({Solution} not found)");
            return 2;
        }
        foreach (var name in args)
        {
            if (!Gates.Contains(name))
            {
                Console.Error.WriteLine($"check: unknown gate \"{name}\"");
                return 2;
            }
        }
        foreach (var name in Gates)
        {
            if (args.Length > 0 && !args.Contains(name))
            {
                continue;
            }
            Console.WriteLine($"==> {name}");
            var error = Restore(name) ?? RunGate(name);
            if (error is not null)
            {
                Console.Out.Flush();
                Console.Error.WriteLine($"{name}: {error}");
                Console.Error.WriteLine($"==> {name}: FAILED");
                return 1;
            }
            Console.WriteLine($"==> {name}: ok");
        }
        Console.WriteLine("==> all gates passed");
        return 0;
    }

    private static string? RunGate(string name)
    {
        switch (name)
        {
            case "format":
                return FormatGate();
            case "lint":
                return LintGate();
            case "semgrep":
                return SemgrepGate();
            case "tests":
                return TestsGate();
            case "docs":
                return DocsGate();
            case "secrets":
                return SecretsGate();
            case "audit":
                return AuditGate();
            case "osv":
                return OsvGate();
            default:
                return $"unknown gate {name}";
        }
    }

    private static bool Flag(string name) => Environment.GetEnvironmentVariable(name) == "1";

    /// <summary>The program's path when it is on PATH (with the Windows executable suffixes).</summary>
    private static string? Which(string program)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        string[] suffixes = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat", ""] : [""];
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            foreach (var suffix in suffixes)
            {
                var candidate = Path.Combine(dir.Length == 0 ? "." : dir, program + suffix);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>Runs a tool from the repository root; returns its exit code and, when asked, its stdout.</summary>
    private static (int Code, string Stdout) Exec(IReadOnlyList<string> argv, bool capture = false)
    {
        var info = new ProcessStartInfo(argv[0]) { UseShellExecute = false, RedirectStandardOutput = capture };
        foreach (var arg in argv.Skip(1))
        {
            info.ArgumentList.Add(arg);
        }
        try
        {
            using var proc = Process.Start(info)!;
            var stdout = capture ? proc.StandardOutput.ReadToEnd() : "";
            proc.WaitForExit();
            return (proc.ExitCode, stdout);
        }
        catch (Win32Exception e)
        {
            return (127, e.Message);
        }
    }

    private static string? Run(IReadOnlyList<string> argv)
    {
        var (code, _) = Exec(argv);
        return code == 0 ? null : $"`{string.Join(' ', argv)}` failed (exit {code})";
    }

    private static List<string>? Dotnet(params string[] args)
    {
        var exe = Which("dotnet");
        return exe is null ? null : [exe, .. args];
    }

    private const string NoDotnet = "dotnet is not installed (https://dotnet.microsoft.com/download)";

    // The gates that build or read the projects; the first of them restores the solution once (locked mode).
    private static readonly string[] DotnetGates = ["format", "lint", "tests", "audit"];
    private static bool _restored;

    /// <summary>A locked-mode restore of the solution, once per run, before the first gate that needs it.</summary>
    private static string? Restore(string gate)
    {
        if (_restored || !DotnetGates.Contains(gate))
        {
            return null;
        }
        var restore = Dotnet("restore", Solution, "--locked-mode", "--nologo", "-v", "quiet");
        if (restore is null)
        {
            return NoDotnet;
        }
        var error = Run(restore);
        if (error is not null)
        {
            return $"{error}: a project changed its packages without its {LockFile}";
        }
        _restored = true;
        return null;
    }

    private static string? FormatGate()
    {
        var format = Dotnet("format", Solution, "--verify-no-changes", "--no-restore");
        if (format is null)
        {
            return NoDotnet;
        }
        var error = Run(format);
        if (error is not null)
        {
            return $"{error}: run `dotnet format {Solution}`";
        }
        Console.WriteLine("dotnet format: every file is formatted");
        return null;
    }

    /// <summary>The build with the analyzers, nullable and code-style rules, warnings as errors.</summary>
    private static string? LintGate()
    {
        var build = Dotnet("build", Solution, "--no-restore", "--nologo", "-v", "quiet");
        if (build is null)
        {
            return NoDotnet;
        }
        var error = Run(build);
        if (error is not null)
        {
            return error;
        }
        Console.WriteLine("dotnet build + analyzers: no warnings");
        return null;
    }

    private static string BuildDir()
    {
        var dir = Environment.GetEnvironmentVariable("FABRICA_BUILD_DIR");
        return Path.GetFullPath(string.IsNullOrEmpty(dir) ? "TestResults" : dir);
    }

    private static string? TestsGate()
    {
        // coverlet's VSTest data collector writes <output>/<run id>/coverage.cobertura.xml. The collector, not
        // coverlet.msbuild: the msbuild integration lost every hit (0 %) when the test host stopped early under load.
        var output = Path.Combine(BuildDir(), "coverage");
        List<string> args =
        [
            "test",
            Solution,
            "--no-restore",
            "--nologo",
            "--collect:XPlat Code Coverage",
            "--results-directory",
            output,
        ];
        if (Flag(CiTier))
        {
            Console.WriteLine("tests: CI tier, the conformance tests run too");
        }
        else
        {
            args.AddRange(["--filter", LocalTierFilter]);
        }
        var test = Dotnet([.. args]);
        if (test is null)
        {
            return NoDotnet;
        }
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }
        var error = Run(test);
        if (error is not null)
        {
            return error;
        }
        var reports = Directory.Exists(output)
            ? Directory.GetFiles(output, "coverage.cobertura.xml", SearchOption.AllDirectories)
            : [];
        if (reports.Length != 1)
        {
            return $"expected one coverage report under {output}, found {reports.Length}";
        }
        var total = LineCoverage(XDocument.Load(reports[0]));
        if (total is null)
        {
            return "the coverage report has no lines";
        }
        Console.WriteLine(Invariant($"Total coverage: {total:F2}%"));
        if (total < MinCoverage)
        {
            return Invariant($"total line coverage {total:F1}% is below {MinCoverage:F0}%");
        }
        Console.WriteLine(Invariant($"tests: line coverage {total:F1}% (minimum {MinCoverage:F0}%)"));
        return null;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>The line coverage percentage of a Cobertura report (lines-covered of lines-valid).</summary>
    private static double? LineCoverage(XDocument report)
    {
        var root = report.Root;
        if (root is null
            || !long.TryParse((string?)root.Attribute("lines-covered"), CultureInfo.InvariantCulture, out var covered)
            || !long.TryParse((string?)root.Attribute("lines-valid"), CultureInfo.InvariantCulture, out var valid)
            || valid == 0)
        {
            return null;
        }
        return 100.0 * covered / valid;
    }

    private static int CountLines(string text)
    {
        var t = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        return t.Length == 0 ? 0 : t.Split('\n').Length;
    }

    /// <summary>Matches one path segment against a glob segment with *, ? and [...] (ranges such as 0-9).</summary>
    private static bool SegmentMatches(string pattern, string name)
    {
        if (pattern.Length == 0)
        {
            return name.Length == 0;
        }
        var p = pattern[0];
        if (p == '*')
        {
            for (var skip = 0; skip <= name.Length; skip++)
            {
                if (SegmentMatches(pattern[1..], name[skip..]))
                {
                    return true;
                }
            }
            return false;
        }
        if (name.Length == 0)
        {
            return false;
        }
        if (p == '?')
        {
            return SegmentMatches(pattern[1..], name[1..]);
        }
        if (p == '[')
        {
            var close = pattern.IndexOf(']', StringComparison.Ordinal);
            if (close < 0)
            {
                return name[0] == '[' && SegmentMatches(pattern[1..], name[1..]);
            }
            var cls = pattern[1..close];
            var c = name[0];
            var hit = false;
            var i = 0;
            while (i < cls.Length)
            {
                if (i + 2 < cls.Length && cls[i + 1] == '-')
                {
                    hit |= cls[i] <= c && c <= cls[i + 2];
                    i += 3;
                }
                else
                {
                    hit |= cls[i] == c;
                    i += 1;
                }
            }
            return hit && SegmentMatches(pattern[(close + 1)..], name[1..]);
        }
        return name[0] == p && SegmentMatches(pattern[1..], name[1..]);
    }

    /// <summary>Whether some regular file under dir matches the remaining glob segments.</summary>
    private static bool GlobHasFile(string dir, ReadOnlySpan<string> segments)
    {
        if (segments.IsEmpty || !Directory.Exists(dir))
        {
            return false;
        }
        var entries = Directory.EnumerateFileSystemEntries(dir).Order(StringComparer.Ordinal).ToList();
        foreach (var entry in entries)
        {
            if (!SegmentMatches(segments[0], Path.GetFileName(entry)))
            {
                continue;
            }
            if (segments.Length == 1 ? File.Exists(entry) : GlobHasFile(entry, segments[1..]))
            {
                return true;
            }
        }
        return false;
    }

    private static string? ReadText(string rel) => File.Exists(rel) ? File.ReadAllText(rel) : null;

    /// <summary>Every violation of docs-manifest.json; an empty list means the gate passes.</summary>
    private static List<string> DocsErrors(JsonElement manifest)
    {
        List<string> errors = [];
        foreach (var rel in Items(manifest, "required"))
        {
            var text = ReadText(rel.GetString() ?? "");
            if (text is null)
            {
                errors.Add($"missing required file: {rel}");
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add($"required file is empty: {rel}");
            }
        }
        foreach (var pattern in Items(manifest, "required_globs"))
        {
            if (!GlobHasFile(".", (pattern.GetString() ?? "").Split('/')))
            {
                errors.Add($"no file matches required pattern: {pattern}");
            }
        }
        foreach (var limit in Members(manifest, "max_lines"))
        {
            var text = ReadText(limit.Name);
            var max = limit.Value.ValueKind == JsonValueKind.Number ? limit.Value.GetInt64() : 0;
            if (text is not null && CountLines(text) > max)
            {
                errors.Add($"{limit.Name} has {CountLines(text)} lines (max {max})");
            }
        }
        foreach (var reference in Members(manifest, "must_reference"))
        {
            var text = ReadText(reference.Name);
            var needle = reference.Value.ToString();
            if (text is not null && !text.Contains(needle, StringComparison.Ordinal))
            {
                errors.Add($"{reference.Name} must reference {needle}");
            }
        }
        return errors;
    }

    private static List<JsonElement> Items(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];

    private static List<JsonProperty> Members(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? [.. value.EnumerateObject()]
            : [];

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ToString()
            : "";

    private static string? DocsGate()
    {
        var raw = ReadText(DocsManifest);
        if (raw is null)
        {
            return $"{DocsManifest} not found";
        }
        using var manifest = JsonDocument.Parse(raw);
        var errors = DocsErrors(manifest.RootElement);
        foreach (var err in errors)
        {
            Console.Error.WriteLine($"docs: {err}");
        }
        if (errors.Count > 0)
        {
            return $"{errors.Count} documentation problem(s)";
        }
        Console.WriteLine($"docs: every file in {DocsManifest} is present");
        return null;
    }

    private static string? SecretsGate()
    {
        var exe = Which("gitleaks");
        if (exe is null)
        {
            if (Flag(AllowMissingGitleaks))
            {
                Console.WriteLine("gitleaks: SKIPPED (not installed)");
                return null;
            }
            return "FAIL - gitleaks is not installed. Install it"
                + " (https://github.com/gitleaks/gitleaks#installing), e.g. `winget install"
                + " Gitleaks.Gitleaks`, `brew install gitleaks` or a release binary, or set "
                + AllowMissingGitleaks
                + "=1 to skip this gate locally.";
        }
        return Run([exe, "dir", ".", "--redact", "--verbose", "--no-banner"]);
    }

    /// <summary>A scanner that is not installed: skipped locally, a failure in CI (FABRICA_CI=1).</summary>
    private static string? MissingScanner(string tool)
    {
        if (Flag(CiTier))
        {
            return $"{tool} not installed (required when {CiTier}=1)";
        }
        Console.WriteLine($"{tool}: skipped: {tool} not installed");
        return null;
    }

    /// <summary>
    /// Semgrep CE with the factory's own rules only; ERROR rules fail and nosemgrep comments are
    /// ignored. Tests and scripts/ are not scanned.
    /// </summary>
    private static string? SemgrepGate()
    {
        var exe = Which("semgrep");
        if (exe is null)
        {
            return MissingScanner("semgrep");
        }
        return Run(
        [
            exe,
            "scan",
            "--config",
            SemgrepRules,
            "--error",
            "--severity",
            "ERROR",
            "--metrics",
            "off",
            "--disable-version-check",
            "--disable-nosem",
            "--quiet",
            "--exclude=scripts",
            "--exclude=tests",
            ".",
        ]);
    }

    /// <summary>
    /// Every NuGet package the projects resolve (top-level and transitive) against the NuGet
    /// advisory database; any known vulnerability fails, like pip-audit or npm audit. Needs the
    /// network; FABRICA_ALLOW_OFFLINE_AUDIT=1 skips it when the advisory feed is unreachable.
    /// </summary>
    private static string? AuditGate()
    {
        var list = Dotnet("list", Solution, "package", "--vulnerable", "--include-transitive", "--format", "json");
        if (list is null)
        {
            return NoDotnet;
        }
        var (code, stdout) = Exec(list, capture: true);
        JsonDocument? report = null;
        try
        {
            report = JsonDocument.Parse(stdout);
        }
        catch (JsonException)
        {
            report = null;
        }
        using (report)
        {
            var problems = report is null
                ? []
                : Items(report.RootElement, "problems").Select(p => Text(p, "text")).ToList();
            if (code != 0 || report is null || problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    Console.Error.WriteLine($"audit: {problem}");
                }
                if (Flag(AllowOfflineAudit))
                {
                    Console.WriteLine("audit: SKIPPED (the NuGet advisory feed is unreachable)");
                    return null;
                }
                return $"`dotnet list package --vulnerable` failed (exit {code}), or set {AllowOfflineAudit}=1 "
                    + "to skip this gate offline";
            }
            List<string> vulnerable = [];
            foreach (var project in Items(report.RootElement, "projects"))
            {
                foreach (var framework in Items(project, "frameworks"))
                {
                    foreach (var kind in new[] { "topLevelPackages", "transitivePackages" })
                    {
                        foreach (var package in Items(framework, kind))
                        {
                            foreach (var advisory in Items(package, "vulnerabilities"))
                            {
                                vulnerable.Add(
                                    $"{Text(package, "id")}@{Text(package, "resolvedVersion")} "
                                        + $"{Text(advisory, "severity")} {Text(advisory, "advisoryurl")}");
                            }
                        }
                    }
                }
            }
            foreach (var line in vulnerable)
            {
                Console.Error.WriteLine($"audit: {line}");
            }
            if (vulnerable.Count > 0)
            {
                return $"{vulnerable.Count} known vulnerabilities in the resolved packages";
            }
        }
        Console.WriteLine("audit: no known vulnerabilities in the resolved NuGet packages");
        return null;
    }

    /// <summary>The advisory groups with CVSS >= 7.0 or no score (unknown counts as high).</summary>
    private static List<string> OsvBlocking(JsonElement report)
    {
        List<string> blocking = [];
        foreach (var result in Items(report, "results"))
        {
            foreach (var pkg in Items(result, "packages"))
            {
                var info = pkg.TryGetProperty("package", out var p) ? p : default;
                foreach (var group in Items(pkg, "groups"))
                {
                    var severity = Text(group, "max_severity").Trim();
                    var known = double.TryParse(severity, NumberStyles.Float, CultureInfo.InvariantCulture, out var score);
                    if (!known || score >= HighCvss)
                    {
                        var ids = string.Join(',', Items(group, "ids").Select(i => i.ToString()));
                        var shown = known ? severity : "unknown";
                        blocking.Add($"{Text(info, "name")}@{Text(info, "version")} {ids} ({shown})");
                    }
                }
            }
        }
        return blocking;
    }

    /// <summary>OSV-Scanner over every packages.lock.json of the library and its tests; high or unknown fails.</summary>
    private static string? OsvGate()
    {
        var exe = Which("osv-scanner");
        if (exe is null)
        {
            return MissingScanner("osv-scanner");
        }
        List<string> argv = [exe, "scan", "source", "--format", "json"];
        foreach (var dir in new[] { "src", "tests" })
        {
            if (Directory.Exists(dir))
            {
                argv.AddRange(
                    Directory.EnumerateFiles(dir, LockFile, SearchOption.AllDirectories)
                        .Order(StringComparer.Ordinal)
                        .Select(f => "--lockfile=" + f.Replace('\\', '/')));
            }
        }
        var (code, stdout) = Exec(argv, capture: true);
        if (code == 0)
        {
            Console.WriteLine($"osv-scanner: no known vulnerabilities in the {LockFile} files");
            return null;
        }
        if (code == OsvNoPackages)
        {
            Console.WriteLine($"osv-scanner: no packages in the {LockFile} files");
            return null;
        }
        if (code != OsvFound)
        {
            return $"osv-scanner failed (exit {code})";
        }
        List<string> blocking;
        try
        {
            using var report = JsonDocument.Parse(stdout);
            blocking = OsvBlocking(report.RootElement);
        }
        catch (JsonException e)
        {
            return $"osv-scanner: unreadable JSON report: {e.Message}";
        }
        foreach (var line in blocking)
        {
            Console.Error.WriteLine($"osv-scanner: {line}");
        }
        if (blocking.Count > 0)
        {
            return $"{blocking.Count} advisory group(s) of high or unknown severity";
        }
        Console.WriteLine("osv-scanner: only advisories below high severity (reported, not failing)");
        return null;
    }
}
