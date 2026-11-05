using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// Shared call-and-assert helpers so every test class speaks to the server the same way.
internal static class ToolCalls
{
    /// The `file` argument in its documented storage form: {"filePath": "<name>"}.
    public static Dictionary<string, object?> File(string name) =>
        new() { ["filePath"] = name };

    public static async Task<CallToolResult> CallAsync(
        McpServerFixture fixture, McpClientTool tool, Dictionary<string, object?> arguments) =>
        await fixture.Client.CallToolAsync(
            tool.Name, arguments, cancellationToken: McpServerFixture.Patience());

    /// Asserts success and returns the response text.
    public static string AssertSuccess(McpServerFixture fixture, CallToolResult response, string what)
    {
        var text = ToolResponse.Text(response);
        Assert.False(response.IsError ?? false, $"[{fixture.Channel}] {what} failed: {text}");
        Assert.DoesNotContain("failed for '", text, StringComparison.Ordinal);
        return text;
    }

    /// Asserts a reported failure: isError set AND the documented per-tool prefix, so a client can
    /// detect it with one flag and a human can read the cause. Returns the response text.
    public static string AssertFailure(McpServerFixture fixture, CallToolResult response, string prefix)
    {
        var text = ToolResponse.Text(response);
        Assert.True(response.IsError ?? false,
            $"[{fixture.Channel}] expected isError=true for a failed operation. Response: {text}");
        Assert.StartsWith(prefix, StripEvaluationNotice(text), StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred invoking", text, StringComparison.Ordinal);
        return text;
    }

    /// The host path of the file a tool says it saved ("Saved to: <path>"), checked to exist.
    public static string SavedFile(McpServerFixture fixture, string responseText)
    {
        var match = Regex.Match(responseText, @"^Saved to:\s*(?<path>.+?)\s*$", RegexOptions.Multiline);
        Assert.True(match.Success, $"[{fixture.Channel}] response names no saved file:\n{responseText}");

        var path = fixture.HostPath(match.Groups["path"].Value);
        Assert.True(System.IO.File.Exists(path), $"[{fixture.Channel}] reported output does not exist: {path}");
        return path;
    }

    /// Storage-relative name of a host path, for passing an output back into another tool.
    public static string StorageName(McpServerFixture fixture, string hostPath) =>
        Path.GetRelativePath(fixture.StoragePath, hostPath).Replace('\\', '/');

    /// Unlicensed runs may lead with a one-line "Evaluation mode: ..." notice.
    public static string StripEvaluationNotice(string text)
    {
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith("Evaluation mode", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        var end = trimmed.IndexOf('\n');
        return end < 0 ? string.Empty : trimmed[(end + 1)..].TrimStart();
    }

    public static IReadOnlySet<string> FilesIn(McpServerFixture fixture) =>
        Directory.EnumerateFiles(fixture.StoragePath, "*", SearchOption.AllDirectories)
            .Select(p => StorageName(fixture, p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
