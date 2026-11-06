using System.Runtime.InteropServices;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// Resolves a command name to an absolute path. On Windows this picks the right
/// extension (.cmd / .exe / .bat / .ps1) for tools like dnx that ship as shims.
internal static class CommandResolver
{
    public static string Resolve(string command)
    {
        if (Path.IsPathRooted(command) && File.Exists(command))
            return command;

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", ".ps1", "" }
            : new[] { "" };

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir.Trim('"'), command + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return command;
    }
}
