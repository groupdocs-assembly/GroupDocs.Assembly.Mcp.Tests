using System.Text.Json;
using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

/// get_document_info: a file as a template, as data, or both.
public class GetDocumentInfoTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GetDocumentInfoTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private async Task<JsonElement> InfoAsync(string file)
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.DocumentInfo, new() { ["file"] = ToolCalls.File(file) });
        ToolCalls.AssertSuccess(_fixture, response, "get_document_info");
        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());
        return json;
    }

    [Fact]
    public async Task WordTemplate_IsATemplate_WithItsDataNameAndTargets()
    {
        var json = await InfoAsync(SampleDocuments.ManagersDocx);

        Assert.Equal("WordProcessing", json.GetProperty("family").GetString());
        Assert.True(json.GetProperty("isTemplate").GetBoolean());
        Assert.Equal(SampleDocuments.ManagersTags, json.GetProperty("tagCount").GetInt32());
        Assert.Equal(SampleDocuments.ManagersName, json.GetProperty("dataSourceNames")[0].GetString());
        var targets = json.GetProperty("canSaveTo").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("pdf", targets);
        Assert.DoesNotContain("xlsx", targets);
        Assert.True(json.GetProperty("size").GetInt64() > 0);
    }

    [Fact]
    public async Task Workbook_AsData_ListsItsTableColumns()
    {
        var json = await InfoAsync(SampleDocuments.ContractsData);

        Assert.False(json.GetProperty("isTemplate").GetBoolean());
        Assert.Equal("table", json.GetProperty("dataKind").GetString());
        var table = json.GetProperty("data").GetProperty("tables")[0];
        Assert.Equal(1, table.GetProperty("number").GetInt32());
        Assert.Equal(SampleDocuments.ContractsColumns, table.GetProperty("columns").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public async Task JsonArray_ReportsItsLength()
    {
        var json = await InfoAsync(SampleDocuments.CustomersJson);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("family").ValueKind);
        Assert.Equal("json", json.GetProperty("dataKind").GetString());
        Assert.Equal("array", json.GetProperty("data").GetProperty("root").GetString());
        Assert.Equal(2, json.GetProperty("data").GetProperty("length").GetInt32());
    }

    [Fact]
    public async Task JsonObject_ListsItsKeys()
    {
        var json = await InfoAsync(SampleDocuments.RecordsJson);
        var keys = json.GetProperty("data").GetProperty("keys").EnumerateArray().ToList();
        Assert.Contains(keys, k => k.GetProperty("name").GetString() == "customers" && k.GetProperty("length").GetInt32() == 3);
    }

    [Fact]
    public async Task Csv_ListsItsHeader()
    {
        var json = await InfoAsync(SampleDocuments.PersonsCsv);
        Assert.Equal(["Name", "Age", "Birth"], json.GetProperty("data").GetProperty("header").EnumerateArray().Select(e => e.GetString()!).ToArray());
        Assert.Equal(3, json.GetProperty("data").GetProperty("rows").GetInt32());
    }

    [Fact]
    public async Task BinaryTemplate_IsTemplateIsUnknown()
    {
        var json = await InfoAsync(SampleDocuments.ContractsMsg);
        Assert.Equal("Email", json.GetProperty("family").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("isTemplate").ValueKind);
    }

    [Fact]
    public async Task UnsupportedFile_IsAFailure()
    {
        await File.WriteAllTextAsync(Path.Combine(_fixture.StoragePath, "notes.bin"), "x");
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.DocumentInfo, new() { ["file"] = ToolCalls.File("notes.bin") });
        ToolCalls.AssertFailure(_fixture, response, "Document-info lookup failed for 'notes.bin'");
    }
}
