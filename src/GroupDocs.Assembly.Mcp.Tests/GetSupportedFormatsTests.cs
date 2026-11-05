using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

public class GetSupportedFormatsTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GetSupportedFormatsTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Catalog_ListsFourFamiliesAndTheDataFormats()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.SupportedFormats, new());
        ToolCalls.AssertSuccess(_fixture, response, "get_supported_formats");
        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());

        var families = json.GetProperty("families").EnumerateArray().ToDictionary(
            f => f.GetProperty("family").GetString()!,
            f => f.GetProperty("saveFormats").EnumerateArray().Select(e => e.GetString()!).ToList());
        Assert.Equal(["WordProcessing", "Spreadsheet", "Presentation", "Email"], families.Keys);
        Assert.Contains("md", families["WordProcessing"]);
        Assert.Contains("pdf", families["Presentation"]);
        Assert.Contains("eml", families["Email"]);

        var kinds = json.GetProperty("dataFormats").EnumerateArray().Select(d => d.GetProperty("kind").GetString()).ToList();
        Assert.Equal(["json", "xml", "csv", "table"], kinds);
    }

    [Fact]
    public async Task WithAFile_ListsThatTemplatesTargets()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.SupportedFormats, new() { ["file"] = ToolCalls.File(SampleDocuments.ContractsXlsx) });
        ToolCalls.AssertSuccess(_fixture, response, "get_supported_formats with a file");
        var json = ToolResponse.Json(response);

        Assert.Equal("Spreadsheet", json.GetProperty("family").GetString());
        var targets = json.GetProperty("saveFormats").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("xlsx", targets);
        Assert.Contains("pdf", targets);
        Assert.DoesNotContain("docx", targets);
    }
}
