using ModelContextProtocol.Client;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// Resolves tool names by keyword. Wire names are the snake_case form of the C# method names
/// (GetDocumentInfo -> get_document_info), so every keyword below is a snake_case substring of
/// exactly one wire name. "documentinfo" would never match "get_document_info" - that trap broke
/// the first Conversion, Comparison and Viewer suites.
internal sealed class ToolCatalog
{
    /// Product tools the server must advertise. Core adds get_license_status on top.
    public const int ProductToolCount = 5;

    public const string LicenseStatusTool = "get_license_status";

    private readonly IReadOnlyList<McpClientTool> _tools;

    private ToolCatalog(IReadOnlyList<McpClientTool> tools) => _tools = tools;

    public static async Task<ToolCatalog> LoadAsync(McpClient client, CancellationToken ct = default)
    {
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return new ToolCatalog(tools.ToList());
    }

    public IReadOnlyList<McpClientTool> All => _tools;

    public McpClientTool AssembleDocument => Resolve("assemble_document");
    public McpClientTool AssemblePerRecord => Resolve("assemble_per_record");
    public McpClientTool TemplateFields => Resolve("template_fields");
    public McpClientTool DocumentInfo => Resolve("document_info");
    public McpClientTool SupportedFormats => Resolve("supported_formats");

    public IEnumerable<McpClientTool> ProductTools =>
        new[] { AssembleDocument, AssemblePerRecord, TemplateFields, DocumentInfo, SupportedFormats };

    private McpClientTool Resolve(string keyword) =>
        _tools.FirstOrDefault(t => t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No tool with name containing '{keyword}'. Found: {string.Join(", ", _tools.Select(t => t.Name))}");
}
