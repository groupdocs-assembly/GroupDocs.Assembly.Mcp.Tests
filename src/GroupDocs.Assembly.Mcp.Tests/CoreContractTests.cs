using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

/// <summary>
/// The input and error contract shared by every GroupDocs MCP server (GroupDocs.Mcp.Core 26.9.0+).
/// </summary>
/// <remarks>
/// One test per shared defect the 2026-08-16 external audit confirmed across the family. Free to
/// run - no licence, no metered key. These assert the PROMISED text, not merely that something
/// went wrong: a loose "IsError || contains(not found)" oracle passes on the opaque error it was
/// meant to catch.
/// </remarks>
public class CoreContractTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CoreContractTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ---- S1: the fileName form the tool descriptions recommend -------------

    [Fact]
    public async Task InfoTool_WithFileNameOnly_Resolves()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await ToolCalls.CallAsync(_fixture, catalog.DocumentInfo, new()
        {
            ["file"] = new Dictionary<string, object?> { ["fileName"] = SampleDocuments.ManagersDocx },
        });

        ToolCalls.AssertSuccess(_fixture, response, "fileName-only input");
    }

    [Fact]
    public async Task AssembleDocument_WithBase64Template_Resolves()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_fixture.StoragePath, SampleDocuments.LetterMd));

        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, new()
        {
            ["template"] = new Dictionary<string, object?>
            {
                ["fileContent"] = Convert.ToBase64String(bytes),
                ["fileName"] = "inline-letter.md",
            },
            ["dataJson"] = SampleDocuments.LetterData,
        });

        var text = ToolCalls.AssertSuccess(_fixture, response, "base64 template");
        Assert.Contains("inline-letter", text, StringComparison.Ordinal);
        Assert.Contains("Ann Lee", OutputText.Read(ToolCalls.SavedFile(_fixture, text)), StringComparison.Ordinal);
    }

    // ---- S2: the available-files listing the descriptions promise ----------

    [Fact]
    public async Task AssembleDocument_MissingTemplate_ReturnsTheAvailableFilesListing()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, new()
        {
            ["template"] = ToolCalls.File("definitely-not-here.docx"),
            ["dataJson"] = "{}",
        });

        var text = ToolResponse.Text(response);
        _output.WriteLine(text);

        Assert.Contains("Available files:", text, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.ManagersDocx, text, StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred invoking", text, StringComparison.Ordinal);

        // S3: a real failure must be flagged, not merely described.
        Assert.True(response.IsError ?? false,
            "A failed operation must set isError so a client can detect it without parsing prose.");
    }

    [Fact]
    public async Task AssembleDocument_MissingDataFile_ReturnsTheAvailableFilesListing()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ManagersDocx),
            ["data"] = ToolCalls.File("no-such-data.json"),
        });

        var text = ToolResponse.Text(response);
        Assert.True(response.IsError ?? false, text);
        Assert.Contains("Available files:", text, StringComparison.Ordinal);
    }

    // ---- S2c: a missing required parameter must be self-correctable --------

    [Fact]
    public async Task InfoTool_WithNoArguments_NamesTheMissingParameter()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await ToolCalls.CallAsync(_fixture, catalog.DocumentInfo, new());

        var text = ToolResponse.Text(response);
        _output.WriteLine(text);

        Assert.True(response.IsError ?? false, "A missing required parameter is a failure.");
        Assert.DoesNotContain("An error occurred invoking", text, StringComparison.Ordinal);
        Assert.Contains("file", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssembleDocument_WithoutTemplate_NamesTheMissingParameter()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, new() { ["dataJson"] = "{}" });

        var text = ToolResponse.Text(response);
        Assert.True(response.IsError ?? false, $"Missing 'template' must be a failure: {text}");
        Assert.Contains("template", text, StringComparison.Ordinal);
    }

    // ---- The Core-shipped status tool --------------------------------------

    [Fact]
    public async Task GetLicenseStatus_IsRegisteredAndDescribesTheAssemblyEngine()
    {
        var response = await _fixture.Client.CallToolAsync(
            ToolCatalog.LicenseStatusTool, new Dictionary<string, object?>(), cancellationToken: McpServerFixture.Patience());

        Assert.False(response.IsError ?? false,
            $"[{_fixture.Channel}] {ToolCatalog.LicenseStatusTool} failed: {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());

        var mode = json.GetProperty("mode").GetString();
        Assert.Contains(mode, new[] { "evaluation", "licensed", "metered" });
        Assert.Equal(mode != "evaluation", json.GetProperty("licensed").GetBoolean());

        // AssemblyLicenseManager must declare EngineMarkerType, or this reports nothing.
        var engine = json.GetProperty("engine");
        Assert.Equal("GroupDocs.Assembly", engine.GetProperty("name").GetString());
        Assert.StartsWith("26.", engine.GetProperty("version").GetString());

        Assert.Equal("GroupDocs.Assembly.Mcp", json.GetProperty("server").GetProperty("name").GetString());
    }
}
