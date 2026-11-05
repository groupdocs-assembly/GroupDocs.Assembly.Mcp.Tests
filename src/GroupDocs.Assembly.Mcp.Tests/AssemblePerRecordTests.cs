using System.Text.RegularExpressions;
using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

/// assemble_per_record: one template, one document per record. The response lists one saved path
/// per line under "Saved to:"; each document must hold its own record's values and no other's.
public class AssemblePerRecordTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AssemblePerRecordTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private async Task<(bool IsError, string Text)> RunAsync(Dictionary<string, object?> args)
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.AssemblePerRecord, args);
        var text = ToolResponse.Text(response);
        _output.WriteLine(text);
        return (response.IsError ?? false, text);
    }

    /// The paths listed under "Saved to:", one per line, up to the failure list or the end.
    private List<string> SavedFiles(string text)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        var start = lines.IndexOf("Saved to:");
        Assert.True(start >= 0, $"No 'Saved to:' list in:\n{text}");
        var files = lines.Skip(start + 1)
            .TakeWhile(l => l.Length > 0 && !Regex.IsMatch(l, @"^\d+ record\(s\) failed:") && !l.StartsWith('…'))
            .Select(_fixture.HostPath).ToList();
        foreach (var f in files) Assert.True(File.Exists(f), $"[{_fixture.Channel}] listed output does not exist: {f}");
        return files;
    }

    [Fact]
    public async Task OneDocumentPerRecord_NamedAfterAField()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.RecordTemplateTxt),
            ["data"] = ToolCalls.File(SampleDocuments.RecordsJson),
            ["fileNameField"] = "Code",
        });

        Assert.False(isError, text);
        Assert.Contains("Assembled 3 of 3 document(s)", text, StringComparison.Ordinal);
        var files = SavedFiles(text);
        Assert.Equal(["C-001.txt", "C-002.txt", "C-003.txt"], files.Select(Path.GetFileName));

        Assert.Equal("Customer C-002: Bob", (await File.ReadAllTextAsync(files[1])).Trim());
        Assert.DoesNotContain("Ann", await File.ReadAllTextAsync(files[2]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlineArray_DefaultNames_AndAnotherFormat()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = $"[{SampleDocuments.LetterData},{SampleDocuments.LetterData.Replace("Ann Lee", "Bo Ek")}]",
            ["format"] = "docx",
        });

        Assert.False(isError, text);
        var files = SavedFiles(text);
        Assert.Equal(["letter_1.docx", "letter_2.docx"], files.Select(Path.GetFileName));
        Assert.Contains("Dear Bo Ek", OutputText.Read(files[1]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordsPath_SelectsTheArray()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.RecordTemplateTxt),
            ["dataJson"] = """{"a":[{"Code":"X","Name":"x"}],"b":[{"Code":"Y","Name":"y"},{"Code":"Z","Name":"z"}]}""",
            ["recordsPath"] = "b",
        });

        Assert.False(isError, text);
        Assert.Contains("Assembled 2 of 2", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeveralArrays_WithoutRecordsPath_AsksWhichOne()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.RecordTemplateTxt),
            ["dataJson"] = """{"a":[{}],"b":[{}]}""",
        });

        Assert.True(isError);
        Assert.StartsWith($"Per-record assembly failed for '{SampleDocuments.RecordTemplateTxt}'", ToolCalls.StripEvaluationNotice(text), StringComparison.Ordinal);
        Assert.Contains("recordsPath", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneBadRecord_TheOthersAreStillGenerated_AndTheFailureIsNamed()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.RecordTemplateTxt),
            ["dataJson"] = """[{"Code":"A","Name":"a"},{"Code":"B"},{"Code":"C","Name":"c"}]""",
        });

        Assert.False(isError, "A partial success is a success; the failure is listed.");
        Assert.Contains("Assembled 2 of 3", text, StringComparison.Ordinal);
        Assert.Contains("record 2:", text, StringComparison.Ordinal);
        Assert.Contains("'Name'", text, StringComparison.Ordinal);
        Assert.Equal(2, SavedFiles(text).Count);
    }

    [Fact]
    public async Task EveryRecordFailing_IsAFailure()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.RecordTemplateTxt),
            ["dataJson"] = """[{"Code":"A"},{"Code":"B"}]""",
        });

        Assert.True(isError, text);
        Assert.Contains("no document was generated", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MoreRecordsThanMaxDocuments_IsRefusedBeforeAnyWork()
    {
        var before = ToolCalls.FilesIn(_fixture);
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.RecordTemplateTxt),
            ["data"] = ToolCalls.File(SampleDocuments.RecordsJson),
            ["maxDocuments"] = 2,
        });

        Assert.True(isError);
        Assert.Contains("3 records, more than maxDocuments=2", text, StringComparison.Ordinal);
        Assert.Equal(before, ToolCalls.FilesIn(_fixture));
    }

    [Fact]
    public async Task NonJsonData_IsRefused()
    {
        var (isError, text) = await RunAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.PersonsTxt),
            ["data"] = ToolCalls.File(SampleDocuments.PersonsCsv),
        });

        Assert.True(isError);
        Assert.Contains("JSON records only", text, StringComparison.Ordinal);
    }
}
