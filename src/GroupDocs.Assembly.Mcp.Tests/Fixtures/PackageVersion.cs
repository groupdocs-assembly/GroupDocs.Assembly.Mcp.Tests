using System.Reflection;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

internal static class PackageVersion
{
    public static string Value { get; } = ResolveVersion();

    public static bool IsLatest => string.IsNullOrWhiteSpace(Value)
        || string.Equals(Value, "latest", StringComparison.OrdinalIgnoreCase);

    private static string ResolveVersion()
    {
        var envOverride = Environment.GetEnvironmentVariable("MCP_PACKAGE_VERSION");
        if (!string.IsNullOrWhiteSpace(envOverride))
            return envOverride;

        var meta = typeof(PackageVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "McpPackageVersion");

        return !string.IsNullOrWhiteSpace(meta?.Value) ? meta!.Value! : "latest";
    }
}
