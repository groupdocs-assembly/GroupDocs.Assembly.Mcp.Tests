using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

/// Failures must come back as isError=true with the tool's documented prefix
/// ("<Operation> failed for '<file>': …"), say what to do about them, write nothing, and leave the
/// server up.
public class ErrorHandlingTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ErrorHandlingTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private async Task<string> AssembleFailsAsync(Dictionary<string, object?> args, string template)
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var before = ToolCalls.FilesIn(_fixture);
        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, args);
        var text = ToolCalls.AssertFailure(_fixture, response, $"Document assembly failed for '{template}'");
        _output.WriteLine(text);
        Assert.Equal(before, ToolCalls.FilesIn(_fixture));
        return text;
    }

    private async Task<string> WriteCorruptedDocxAsync()
    {
        const string name = "corrupted.docx";
        // A ZIP local-file-header signature followed by garbage: looks like OOXML, is not.
        await File.WriteAllBytesAsync(
            Path.Combine(_fixture.StoragePath, name),
            new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03 });
        return name;
    }

    [Fact]
    public async Task AssembleDocument_CorruptedTemplate_ReportsFailureAndServerStaysUp()
    {
        var corrupted = await WriteCorruptedDocxAsync();

        await AssembleFailsAsync(new() { ["template"] = ToolCalls.File(corrupted), ["dataJson"] = "{}" }, corrupted);

        var after = await _fixture.Client.ListToolsAsync();
        Assert.NotEmpty(after);
    }

    [Fact]
    public async Task GetDocumentInfo_CorruptedDocument_ReportsFailureWithPrefix()
    {
        // A .docx is also a possible data document; reading its tables fails on garbage.
        var corrupted = await WriteCorruptedDocxAsync();
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await ToolCalls.CallAsync(_fixture, catalog.DocumentInfo, new() { ["file"] = ToolCalls.File(corrupted) });

        ToolCalls.AssertFailure(_fixture, response, $"Document-info lookup failed for '{corrupted}'");
    }

    [Fact]
    public async Task MissingField_IsNamed_AndAllowMissingMembersIsOffered()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = """{"customer":{"Nmae":"typo"},"order":{"Number":"1","Total":1,"Items":[{"Title":"x","Qty":1}]}}""",
        }, SampleDocuments.LetterMd);

        Assert.Contains("'Name'", text, StringComparison.Ordinal);
        Assert.Contains("allowMissingMembers=true", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnclosedForeach_SaysWhichTagIsMissing()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.UnclosedTxt),
            ["dataJson"] = """{"items":[{"Title":"x"}]}""",
        }, SampleDocuments.UnclosedTxt);

        Assert.Contains("<</foreach>>", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoData_IsRefused_WithTheTwoWaysToPassIt()
    {
        var text = await AssembleFailsAsync(new() { ["template"] = ToolCalls.File(SampleDocuments.LetterMd) }, SampleDocuments.LetterMd);
        Assert.Contains("dataJson", text, StringComparison.Ordinal);
        Assert.Contains("'data'", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DataGivenTwice_IsRefused()
    {
        await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ManagersDocx),
            ["data"] = ToolCalls.File(SampleDocuments.ManagersJson),
            ["dataJson"] = "[]",
        }, SampleDocuments.ManagersDocx);
    }

    [Fact]
    public async Task InvalidJson_IsReported()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = "{ not json",
        }, SampleDocuments.LetterMd);
        Assert.Contains("JSON", text, StringComparison.Ordinal);
    }

    /// The engine only discovers a wrong target after assembling the whole template; the server
    /// refuses before, and lists what the template can be saved as.
    [Fact]
    public async Task TargetOutsideTheTemplatesFamily_IsRefusedWithTheTargets()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ManagersDocx),
            ["data"] = ToolCalls.File(SampleDocuments.ManagersJson),
            ["format"] = "xlsx",
        }, SampleDocuments.ManagersDocx);

        Assert.Contains("cannot be saved as 'xlsx'", text, StringComparison.Ordinal);
        Assert.Contains("pdf", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownTargetFormat_IsRefused()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = SampleDocuments.LetterData,
            ["format"] = "not-a-format",
        }, SampleDocuments.LetterMd);
        Assert.Contains("not-a-format", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedDataFormat_IsRefused()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["data"] = ToolCalls.File(SampleDocuments.ReadmeMd),
        }, SampleDocuments.LetterMd);
        Assert.Contains("not a data format", text, StringComparison.Ordinal);
    }

    /// A binary template's tags cannot be read, so the name of a collection cannot be worked out.
    [Fact]
    public async Task BinaryTemplate_WithCollectionData_AsksForTheDataSourceName()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsMsg),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
        }, SampleDocuments.ContractsMsg);
        Assert.Contains("dataSourceName", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TableNumberOutOfRange_IsReportedOneBased()
    {
        var text = await AssembleFailsAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsDocx),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
            ["dataTable"] = 5,
        }, SampleDocuments.ContractsDocx);
        Assert.Contains("no table 5", text, StringComparison.Ordinal);
    }
}
