using System.Text.Json;
using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

/// get_template_fields: what a template needs, read without the engine. The strongest check is the
/// round trip: a skeleton filled in and passed back to assemble_document must generate the document.
public class GetTemplateFieldsTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GetTemplateFieldsTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private async Task<JsonElement> FieldsAsync(string template)
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.TemplateFields, new() { ["template"] = ToolCalls.File(template) });
        ToolCalls.AssertSuccess(_fixture, response, "get_template_fields");
        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());
        return json;
    }

    private static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToList();

    [Fact]
    public async Task NestedTemplate_ListsNamesFieldsAndASkeleton()
    {
        var json = await FieldsAsync(SampleDocuments.ManagersDocx);

        Assert.True(json.GetProperty("readable").GetBoolean());
        Assert.Equal("WordProcessing", json.GetProperty("family").GetString());
        Assert.Equal(SampleDocuments.ManagersTags, json.GetProperty("tagCount").GetInt32());
        Assert.Equal([SampleDocuments.ManagersName], Strings(json.GetProperty("dataSourceNames")));
        Assert.Contains("managers[].Contract[].Client.Name", Strings(json.GetProperty("fields")));
        Assert.Equal(JsonValueKind.Array, json.GetProperty("dataSkeleton").GetProperty("managers").ValueKind);
        Assert.Equal("foreach", json.GetProperty("tags")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task WorkbookTemplate_NamesTheWorksheet()
    {
        var json = await FieldsAsync(SampleDocuments.ContractsXlsx);
        Assert.Equal([SampleDocuments.ContractsName], Strings(json.GetProperty("dataSourceNames")));
        Assert.Equal("worksheet 1", json.GetProperty("tags")[0].GetProperty("part").GetString());
    }

    [Fact]
    public async Task TagsSpanningParagraphs_AreRead()
    {
        // The PPTX's foreach is split over two paragraphs, the DOCX's over three.
        Assert.Equal([SampleDocuments.ContractsName], Strings((await FieldsAsync(SampleDocuments.ContractsPptx)).GetProperty("dataSourceNames")));
        Assert.Equal([SampleDocuments.ContractsByManagerName], Strings((await FieldsAsync(SampleDocuments.ContractsByManagerDocx)).GetProperty("dataSourceNames")));
    }

    [Fact]
    public async Task BinaryTemplate_IsNotReadable_AndSaysWhatToDo()
    {
        var json = await FieldsAsync(SampleDocuments.ContractsMsg);
        Assert.False(json.GetProperty("readable").GetBoolean());
        Assert.Equal("Email", json.GetProperty("family").GetString());
        Assert.Contains("DOCX", json.GetProperty("notes")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MustacheSyntax_IsFlagged()
    {
        var json = await FieldsAsync(SampleDocuments.MustacheTxt);
        Assert.Equal(0, json.GetProperty("tagCount").GetInt32());
        Assert.Contains(json.GetProperty("notes").EnumerateArray(), n => n.GetString()!.Contains("{{…}}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Skeleton_FilledIn_AssemblesTheTemplate()
    {
        var json = await FieldsAsync(SampleDocuments.LetterMd);
        var skeleton = json.GetProperty("dataSkeleton").GetRawText();

        // An agent fills the placeholders; here the skeleton is used as-is, which must already be valid data.
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = skeleton,
            ["outputName"] = "from-skeleton",
        });

        var text = ToolCalls.AssertSuccess(_fixture, response, "assemble_document with the skeleton");
        Assert.Contains("Your order", OutputText.Read(ToolCalls.SavedFile(_fixture, text)), StringComparison.Ordinal);
    }
}
