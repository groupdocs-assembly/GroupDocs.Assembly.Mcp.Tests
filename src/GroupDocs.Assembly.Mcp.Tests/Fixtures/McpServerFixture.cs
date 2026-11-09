using ModelContextProtocol.Client;
using Xunit;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// Boots the GroupDocs.Assembly.Mcp server as a child process, wires an MCP stdio client, and
/// seeds a temporary storage folder with sample documents.
///
/// Used as IClassFixture&lt;McpServerFixture&gt; - ONE fresh server process PER TEST CLASS - so no
/// single process can exhaust an evaluation-mode document cap, and so one class's output files
/// never leak into another class's assertions.
///
/// Three launch channels, picked by environment variable - the first one set wins:
///
///   MCP_SERVER_IMAGE  docker run of that image (e.g. ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0).
///                     The storage folder is bind-mounted at /data, so host-side assertions see
///                     exactly what the container wrote.
///   MCP_SERVER_DLL    dotnet &lt;dll&gt;, for testing a local build before it is published.
///   (neither)         dnx against the published NuGet package (the default).
///
/// Metered keys reach the server only through <see cref="MeteredServerFixture"/>. Every other
/// class runs with the keys removed - a child process otherwise inherits them from this
/// process, and a developer whose machine holds the keys would bill the account on every run.
public class McpServerFixture : IAsyncLifetime
{
    private readonly bool _forwardMeteredKeys;

    public McpServerFixture() : this(forwardMeteredKeys: false) { }

    protected McpServerFixture(bool forwardMeteredKeys) => _forwardMeteredKeys = forwardMeteredKeys;

    public const string ContainerStorage = "/data";
    private const string ContainerLicenseDir = "/license";

    /// Points the suite at a locally built server DLL instead of the published NuGet.
    public const string LocalServerVariable = "MCP_SERVER_DLL";

    /// Points the suite at a container image instead of the published NuGet.
    public const string ServerImageVariable = "MCP_SERVER_IMAGE";

    public string StoragePath { get; } = Path.Combine(
        Path.GetTempPath(),
        $"gdasm-mcp-it-{Guid.NewGuid():N}");

    public string PackageVersionUnderTest => PackageVersion.Value;

    /// Which launch channel this run used - named in failures so a red test says which
    /// artifact it was actually testing.
    public string Channel { get; private set; } = "dnx";

    public bool IsDocker => Channel.StartsWith("docker:", StringComparison.Ordinal);

    public McpClient Client { get; private set; } = null!;

    /// A token for tool calls that are legitimately slow. The first call into the engine loads
    /// the Aspose stack (Words, Cells, Slides, PDF) and can take tens of seconds on a cold runner.
    public static CancellationToken Patience(TimeSpan? timeout = null) =>
        new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(3)).Token;

    private static string? ServerImage => NullIfBlank(Environment.GetEnvironmentVariable(ServerImageVariable));
    private static string? ServerDll => NullIfBlank(Environment.GetEnvironmentVariable(LocalServerVariable));
    private static string? LicensePath => NullIfBlank(Environment.GetEnvironmentVariable("GROUPDOCS_LICENSE_PATH"));

    private static readonly string[] MeteredVariables =
    {
        MeteredKeys.PublicKeyVariable,
        MeteredKeys.PrivateKeyVariable,
    };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoragePath);
        SampleDocuments.WriteAll(StoragePath);
        SampleDocuments.CopyRealSamples(StoragePath, SampleDocuments.ResolveSourceSampleDocs());

        var transport = new StdioClientTransport(BuildTransportOptions());

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        Client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
    }

    /// Maps the path a tool reported ("Saved to: ...") to a path on this host. On the docker
    /// channel the server reports /data/..., which is the bind-mounted StoragePath.
    public string HostPath(string reportedPath)
    {
        var normalized = reportedPath.Trim().Replace('\\', '/');
        if (normalized.StartsWith(ContainerStorage + "/", StringComparison.Ordinal))
            return Path.Combine(StoragePath, normalized[(ContainerStorage.Length + 1)..]);

        return Path.IsPathRooted(reportedPath)
            ? reportedPath.Trim()
            : Path.Combine(StoragePath, reportedPath.Trim());
    }

    private StdioClientTransportOptions BuildTransportOptions()
    {
        if (ServerImage is { } image)
        {
            Channel = $"docker:{image}";
            return new StdioClientTransportOptions
            {
                Name = "groupdocs-assembly-mcp",
                Command = CommandResolver.Resolve("docker"),
                Arguments = BuildDockerArguments(image),
                WorkingDirectory = StoragePath,
                // `docker -e NAME` reads the value from THIS process's environment, so no key is
                // ever written into an argument.
                EnvironmentVariables = BuildServerEnv(),
            };
        }

        if (ServerDll is { } dll)
        {
            if (!File.Exists(dll))
                throw new InvalidOperationException(
                    $"{LocalServerVariable} is set to '{dll}', but no such file exists. Build the " +
                    "server project first, or unset the variable to test the published package.");

            Channel = $"dll:{Path.GetFileName(dll)}";
            return new StdioClientTransportOptions
            {
                Name = "groupdocs-assembly-mcp",
                Command = CommandResolver.Resolve("dotnet"),
                Arguments = new[] { dll },
                WorkingDirectory = StoragePath,
                EnvironmentVariables = BuildServerEnv(),
            };
        }

        // dnx has no `@latest` literal - to get the latest stable, omit the `@<version>` entirely.
        var packageSpec = PackageVersion.IsLatest
            ? "GroupDocs.Assembly.Mcp"
            : $"GroupDocs.Assembly.Mcp@{PackageVersion.Value}";

        Channel = $"dnx:{packageSpec}";
        return new StdioClientTransportOptions
        {
            Name = "groupdocs-assembly-mcp",
            Command = CommandResolver.Resolve("dnx"),
            Arguments = new[] { packageSpec, "--yes" },
            WorkingDirectory = StoragePath,
            EnvironmentVariables = BuildServerEnv(),
        };
    }

    /// `docker run --rm -i` plus the storage mount, the in-container paths, and -e forwarding
    /// for whichever licensing variables this environment actually has.
    private string[] BuildDockerArguments(string image)
    {
        var args = new List<string> { "run", "--rm", "-i" };

        // The image runs as a non-root user (uid 1654). On a Linux host a bind mount keeps its
        // host owner, so a folder the runner owns is read-only to that user and every write into
        // /data fails with "Permission denied". Run as the host user, exactly as the README tells
        // Linux users to. Docker Desktop on Windows and macOS does not enforce bind-mount
        // ownership, so the flag is Linux-only.
        if (OperatingSystem.IsLinux() && HostUser() is { } user)
        {
            args.Add("--user");
            args.Add(user);
        }

        args.AddRange(new[]
        {
            "-v", $"{StoragePath}:{ContainerStorage}",
            "-e", $"GROUPDOCS_MCP_STORAGE_PATH={ContainerStorage}",
        });

        foreach (var name in _forwardMeteredKeys ? MeteredVariables : Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                continue;

            args.Add("-e");
            args.Add(name);              // name only: the value never reaches the command line
        }

        if (LicensePath is { } license && File.Exists(license))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(license))!;
            args.Add("-v");
            args.Add($"{dir}:{ContainerLicenseDir}:ro");
            args.Add("-e");
            args.Add($"GROUPDOCS_LICENSE_PATH={ContainerLicenseDir}/{Path.GetFileName(license)}");
        }

        args.Add(image);
        return args.ToArray();
    }

    /// "uid:gid" of the current process, from `id`, or null when it cannot be read.
    private static string? HostUser()
    {
        static string? Id(string flag)
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("id", flag)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                });
                var value = p!.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return p.ExitCode == 0 && value.Length > 0 ? value : null;
            }
            catch
            {
                return null;
            }
        }

        var uid = Id("-u");
        var gid = Id("-g");
        return uid is null || gid is null ? null : $"{uid}:{gid}";
    }

    private Dictionary<string, string?> BuildServerEnv()
    {
        var env = new Dictionary<string, string?>
        {
            ["GROUPDOCS_MCP_STORAGE_PATH"] = StoragePath,
            ["DOTNET_NOLOGO"] = "true",
            // A null value removes the variable from what the child inherits. Outputs must land in
            // StoragePath, where the assertions look; GitHub credentials have no business in a
            // package pulled from nuget.org.
            ["GROUPDOCS_MCP_OUTPUT_PATH"] = null,
            ["GITHUB_TOKEN"] = null,
            ["GH_TOKEN"] = null,
            [MeteredKeys.PublicKeyVariable] = null,
            [MeteredKeys.PrivateKeyVariable] = null,
        };

        // Forwarded only to the metered class, and only when BOTH are present. One alone exercises
        // the server's half-configured fallback, not metered licensing. Values are never logged.
        if (_forwardMeteredKeys && MeteredKeys.Configured)
        {
            env[MeteredKeys.PublicKeyVariable] = MeteredKeys.PublicKey;
            env[MeteredKeys.PrivateKeyVariable] = MeteredKeys.PrivateKey;
        }

        // On the docker channel the in-container licence path is set by -e instead.
        if (ServerImage is null && LicensePath is { } licensePath)
            env["GROUPDOCS_LICENSE_PATH"] = licensePath;

        return env;
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Client is not null)
                await Client.DisposeAsync();
        }
        catch
        {
            // Swallow disposal errors - they must not mask test failures.
        }

        try
        {
            if (Directory.Exists(StoragePath))
                Directory.Delete(StoragePath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup on Windows where handles may linger briefly.
        }
    }
}

/// The fixture for <c>MeteredLicensingTests</c>: the only one that forwards the metered key pair.
public sealed class MeteredServerFixture() : McpServerFixture(forwardMeteredKeys: true);
