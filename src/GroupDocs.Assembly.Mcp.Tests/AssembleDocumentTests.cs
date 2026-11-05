using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

/// assemble_document: a template plus data in, one generated document out. Every test reads the
/// output back and checks that values from the data are in it - a document that merely exists
/// proves nothing about the assembly.
public class AssembleDocumentTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AssembleDocumentTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private async Task<(string Text, string Output)> AssembleAsync(Dictionary<string, object?> args)
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        var response = await ToolCalls.CallAsync(_fixture, catalog.AssembleDocument, args);
        var text = ToolCalls.AssertSuccess(_fixture, response, "assemble_document");
        _output.WriteLine(text);
        return (text, ToolCalls.SavedFile(_fixture, text));
    }

    [Fact]
    public async Task JsonArray_FillsNestedLists_AndIsNamedFromTheTemplate()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ManagersDocx),
            ["data"] = ToolCalls.File(SampleDocuments.ManagersJson),
        });

        Assert.Contains($"JSON array as '{SampleDocuments.ManagersName}'", text, StringComparison.Ordinal);
        Assert.EndsWith("managers_assembled.docx", output, StringComparison.Ordinal);
        var body = OutputText.Read(output);
        Assert.Contains(SampleDocuments.Manager1, body, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.Manager3, body, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.Client10, body, StringComparison.Ordinal);
        Assert.DoesNotContain("<<", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task XmlData_SavedAsPdf()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ManagersDocx),
            ["data"] = ToolCalls.File(SampleDocuments.ManagersXml),
            ["format"] = "pdf",
        });

        Assert.Contains("XML as 'managers'", text, StringComparison.Ordinal);
        Assert.True(OutputText.IsPdf(output), "The output is not a PDF.");
    }

    [Fact]
    public async Task CsvData_IntoATextTemplate_WithAnAggregate()
    {
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.PersonsTxt),
            ["data"] = ToolCalls.File(SampleDocuments.PersonsCsv),
        });

        var body = OutputText.Read(output);
        Assert.Contains(SampleDocuments.Person1Line, body, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.PersonsAverage, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpreadsheetTable_IntoAWorkbookTemplate()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsXlsx),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
        });

        Assert.Contains("Client, Manager, Price", text, StringComparison.Ordinal);
        var body = OutputText.Read(output);
        Assert.Contains(SampleDocuments.Client1, body, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.Client10, body, StringComparison.Ordinal);
    }

    /// The presentation engine draws through System.Drawing.Common: on Linux this needs libgdiplus and
    /// the EnableUnixSupport switch the server ships with. The PPTX's foreach also spans two paragraphs,
    /// which the server must read to name the data.
    [Fact]
    public async Task PresentationTemplate_SavedAsPdf()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsPptx),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
            ["format"] = "pdf",
        });

        Assert.Contains($"as '{SampleDocuments.ContractsName}'", text, StringComparison.Ordinal);
        Assert.True(OutputText.IsPdf(output), "The output is not a PDF.");
    }

    [Fact]
    public async Task PresentationTemplate_KeepsItsFormat()
    {
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsPptx),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
        });

        Assert.Contains(SampleDocuments.Client1, OutputText.Read(output), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupedTemplate_TypedOverSeveralLines()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsByManagerDocx),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
        });

        Assert.Contains($"as '{SampleDocuments.ContractsByManagerName}'", text, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.Manager3, OutputText.Read(output), StringComparison.Ordinal);
    }

    /// Customers.json carries Json.NET "$id"/"$ref" metadata, which the engine's strict JSON mode cannot read.
    [Fact]
    public async Task JsonWithReferenceMetadata_IntoAnHtmlTemplate()
    {
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.CommonListHtml),
            ["data"] = ToolCalls.File(SampleDocuments.CustomersJson),
        });

        var body = OutputText.Read(output);
        Assert.Contains(SampleDocuments.Customer1, body, StringComparison.Ordinal);
        Assert.Contains(SampleDocuments.Customer2, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlineJsonObject_KeysAreTheTemplatesNames_AndCodesKeepLeadingZeros()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = SampleDocuments.LetterData,
        });

        Assert.Contains("inline JSON (JSON object)", text, StringComparison.Ordinal);
        var body = OutputText.Read(output);
        Assert.Contains("Dear Ann Lee", body, StringComparison.Ordinal);
        Assert.Contains("order 007 ", body, StringComparison.Ordinal);
        Assert.Contains("1,234.50", body, StringComparison.Ordinal);
        Assert.Contains("- Lamp x 2", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkdownTemplate_SavedAsDocx_WithACallerName()
    {
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ReadmeMd),
            ["dataJson"] = """{"product":"Acme Widget","description":"Generated by an agent."}""",
            ["format"] = "docx",
            ["outputName"] = "acme-readme",
        });

        Assert.EndsWith("acme-readme.docx", output, StringComparison.Ordinal);
        var body = OutputText.Read(output);
        Assert.Contains("Acme Widget", body, StringComparison.Ordinal);
        Assert.Contains("Generated by an agent.", body, StringComparison.Ordinal);
    }

    /// Evaluation mode replaces an e-mail's HTML body (where this MSG keeps its table) with the
    /// evaluation line, so the table can only be checked when licensed. Unlicensed, the oracle is a
    /// well-formed MIME message - the explicit name is what this test is about.
    [Fact]
    public async Task ExplicitDataSourceName_ForATemplateWhoseTagsCannotBeRead()
    {
        var licensed = await IsLicensedAsync();
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.ContractsMsg),
            ["data"] = ToolCalls.File(SampleDocuments.ContractsData),
            ["dataSourceName"] = SampleDocuments.ContractsName,
            ["format"] = "eml",
        });

        var message = OutputText.Read(output);
        Assert.Contains("Content-Type: multipart/alternative", message, StringComparison.Ordinal);
        if (licensed) Assert.Contains(SampleDocuments.Client1, OutputText.DecodeQuotedPrintable(message), StringComparison.Ordinal);
    }

    /// An e-mail template's plain-text body keeps the data in both modes.
    [Fact]
    public async Task EmailTemplate_FillsSubjectAndBody()
    {
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterEml),
            ["dataJson"] = """{"customer":{"Name":"Ann","Code":"007"}}""",
        });

        Assert.EndsWith(".eml", output, StringComparison.Ordinal);
        Assert.Contains("Dear Ann, your code is 007.", OutputText.DecodeQuotedPrintable(OutputText.Read(output)), StringComparison.Ordinal);
    }

    private async Task<bool> IsLicensedAsync() =>
        ToolResponse.Json(await _fixture.Client.CallToolAsync(
                ToolCatalog.LicenseStatusTool, new Dictionary<string, object?>(), cancellationToken: McpServerFixture.Patience()))
            .GetProperty("licensed").GetBoolean();

    /// Image tags accept base64 data. HTML output keeps its images in a folder beside the page, which
    /// the server must store too, or the page shows broken images.
    [Fact]
    public async Task HtmlOutput_StoresItsImageFolder()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.PhotoDocx),
            ["dataJson"] = $$"""{"expression":"{{SampleDocuments.PixelPngBase64}}"}""",
            ["format"] = "html",
        });

        Assert.Contains("images in '", text, StringComparison.Ordinal);
        var folder = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "_files");
        Assert.True(Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any(), $"No image folder at {folder}");
        var html = await File.ReadAllTextAsync(output);
        Assert.Contains(Path.GetFileName(folder) + "/", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowMissingMembers_OutputsNothingForMissingFields()
    {
        var (_, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = """{"customer":{},"order":{"Number":"9","Total":0,"Items":[]}}""",
            ["allowMissingMembers"] = true,
        });

        Assert.Contains("Your order 9 ", OutputText.Read(output), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlineErrors_WritesTheErrorIntoTheOutput_AndSaysSo()
    {
        var (text, output) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.InlineErrorDocx),
            ["dataJson"] = """{"product":[{"ProductName":"Desk"}]}""",
            ["inlineErrors"] = true,
        });

        Assert.Contains("The template has errors", text, StringComparison.Ordinal);
        Assert.Contains("Error!", OutputText.Read(output), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnlicensedRuns_LeadWithTheEvaluationNotice()
    {
        var licensed = await IsLicensedAsync();

        var (text, _) = await AssembleAsync(new()
        {
            ["template"] = ToolCalls.File(SampleDocuments.LetterMd),
            ["dataJson"] = SampleDocuments.LetterData,
            ["outputName"] = "notice-check",
        });

        Assert.Equal(!licensed, text.StartsWith("Evaluation mode:", StringComparison.Ordinal));
    }
}
