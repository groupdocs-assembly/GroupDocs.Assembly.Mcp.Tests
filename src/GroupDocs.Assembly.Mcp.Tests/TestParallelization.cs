using Xunit;

// Each test class gets its own fresh MCP server (IClassFixture<McpServerFixture>) so a
// single long-lived server never exhausts the engine's evaluation-mode document-open
// cap (the failure mode that broke GroupDocs.Metadata: "Could not open more than N
// document files in evaluation mode"). Serialise the classes so those per-class dnx
// servers start one at a time rather than all at once on a CI runner.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
