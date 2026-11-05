using GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests;

public class ToolDiscoveryTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ToolDiscoveryTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void ServerInfo_AdvertisesGroupDocsAssemblyMcp()
    {
        var info = _fixture.Client.ServerInfo;

        Assert.NotNull(info);
        Assert.Equal("GroupDocs.Assembly.Mcp", info!.Name);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));

        _output.WriteLine($"Server: {info.Name} {info.Version}  (channel: {_fixture.Channel}, package under test: {_fixture.PackageVersionUnderTest})");
    }

    [Fact]
    public async Task ListTools_ExposesEveryAssemblyToolAndLicenseStatus()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        foreach (var tool in catalog.All)
            _output.WriteLine($"tool: {tool.Name} — {tool.Description}");

        // Five product tools plus get_license_status, which GroupDocs.Mcp.Core registers on
        // every server from 26.9.0.
        Assert.Equal(ToolCatalog.ProductToolCount + 1, catalog.All.Count);
        Assert.Contains(catalog.All, t => t.Name == ToolCatalog.LicenseStatusTool);

        var names = catalog.ProductTools.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public async Task AllTools_HaveNonEmptyDescriptionAndObjectInputSchema()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        Assert.NotEmpty(catalog.All);
        foreach (var tool in catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), $"Tool '{tool.Name}' has no description.");

            var schema = tool.JsonSchema;
            Assert.True(schema.ValueKind == System.Text.Json.JsonValueKind.Object, $"Tool '{tool.Name}' has no object input schema.");
            Assert.True(schema.TryGetProperty("properties", out _), $"Tool '{tool.Name}' schema missing 'properties'.");
        }
    }

    /// The description is how an agent picks a tool and reads its result. Each product tool must
    /// say what it returns, name the prefix its failures start with, and tell the agent not to
    /// pre-check files.
    [Fact]
    public async Task ProductTools_DescriptionsCarryTheAgentContract()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        foreach (var tool in catalog.ProductTools)
        {
            var d = tool.Description ?? string.Empty;
            Assert.Contains("failed for", d, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Returns", d, StringComparison.Ordinal);
            Assert.Contains("Do NOT pre-check", d, StringComparison.Ordinal);
        }
    }

    /// The generating tools take the data inline or as a file, and point agents at the template
    /// inspection tool, which is how an agent learns what to put in the data.
    [Fact]
    public async Task GeneratingTools_AcceptInlineOrFileData_AndPointAtTemplateFields()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        foreach (var tool in new[] { catalog.AssembleDocument, catalog.AssemblePerRecord })
        {
            var properties = tool.JsonSchema.GetProperty("properties");
            foreach (var name in new[] { "template", "dataJson", "data", "dataSourceName", "format" })
                Assert.True(properties.TryGetProperty(name, out _), $"{tool.Name} has no '{name}' parameter.");
        }
        Assert.Contains("get_template_fields", catalog.AssembleDocument.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectionTools_AreReadOnly()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        foreach (var tool in new[] { catalog.TemplateFields, catalog.DocumentInfo, catalog.SupportedFormats })
            Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint ?? false, $"{tool.Name} should be annotated read-only.");
    }
}
