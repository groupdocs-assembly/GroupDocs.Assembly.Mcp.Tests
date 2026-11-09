# AGENTS.md — Guide for AI coding agents

Brief orientation for AI coding agents (Claude Code, Copilot, Cursor, Aider, Amp, Codex) working in this repository.

## What this repo is

**Integration tests** for the [`GroupDocs.Assembly.Mcp`](https://www.nuget.org/packages/GroupDocs.Assembly.Mcp) package — an MCP server that exposes GroupDocs.Assembly for .NET as AI-callable tools for generating documents from templates and data.

This repo is **not** the server. The server lives at [groupdocs-assembly/GroupDocs.Assembly.Mcp](https://github.com/groupdocs-assembly/GroupDocs.Assembly.Mcp). This repo:

1. Consumes only a **built artifact** — the published NuGet package (default), a container image, or a server DLL. Never a project reference.
2. Launches the server, connects as an MCP stdio client, and exercises every advertised tool.
3. Doubles as a copy-pasteable set of example configs and how-to guides for every deployment channel.

## Tools under test

| Wire name | What it does |
|---|---|
| `assemble_document` | Template + data (inline JSON, or a JSON/XML/CSV file or a document table) → one generated document |
| `assemble_per_record` | One document per element of a JSON array; failed records listed |
| `get_template_fields` | Tags, data names, fields and a JSON skeleton of a template (JSON) |
| `get_document_info` | A file as a template (family, tags, save targets) and as data (keys, columns, tables) (JSON) |
| `get_supported_formats` | Template formats, save targets per family, data formats (JSON) |
| `get_license_status` | Shipped by GroupDocs.Mcp.Core: licensing mode, metered consumption, engine version |

## Folder layout

```
src/GroupDocs.Assembly.Mcp.Tests/
  Fixtures/
    McpServerFixture.cs     ← launches the server (dnx | MCP_SERVER_DLL | MCP_SERVER_IMAGE), wires the stdio client
    SampleDocuments.cs      ← real fixtures from Files/ + synthetic templates; every phrase constant is really produced
    OutputText.cs           ← reads a generated DOCX/XLSX/PPTX/HTML/text output back; PDF signature; quoted-printable
    ToolCatalog.cs          ← snake_case keyword resolution of tool names; expected tool count
    ToolCalls.cs            ← call helper, success/failure assertions, "Saved to:" path parsing (incl. /data mapping)
    ToolResponse.cs         ← CallToolResult text/JSON extraction
    MeteredKeys.cs          ← metered key pair from the environment; hard-fails when absent
    CommandResolver.cs      ← cross-platform dnx.cmd / docker resolution
    PackageVersion.cs       ← version from env / assembly metadata / default
  ToolDiscoveryTests.cs     ← handshake, tool list, description + schema contract
  CoreContractTests.cs      ← fileName form, base64 template, missing-file listing, isError, get_license_status
  AssembleDocumentTests.cs  AssemblePerRecordTests.cs  GetTemplateFieldsTests.cs
  GetDocumentInfoTests.cs   GetSupportedFormatsTests.cs  ErrorHandlingTests.cs
  MeteredLicensingTests.cs  ← Category=Metered; bills a real account
  TestParallelization.cs    ← one server per class, classes run serially
Files/                      ← real fixtures + README.md with provenance
.github/workflows/          ← integration.yml (dnx × 3 OS + docker job), metered.yml (dispatch/weekly)
changelog/  how-to/  examples/  docker-scripts/
Directory.Build.props       ← McpPackageVersion (single source of "what version are we testing")
```

## Commands you can run

```bash
dotnet build -c Release
dotnet test -c Release --filter "Category!=Metered"                       # published package
MCP_SERVER_DLL=/path/to/GroupDocs.Assembly.Mcp.dll dotnet test -c Release --filter "Category!=Metered"
MCP_SERVER_IMAGE=ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0 dotnet test -c Release --filter "Category!=Metered"
dotnet test -c Release --filter "FullyQualifiedName~ToolDiscovery"      # fastest: nothing is generated
```

## Key design decisions

1. **One server per test class** (`IClassFixture<McpServerFixture>`, serial classes), so one class's outputs never leak into another's assertions.
2. **Outputs are located from the response.** Tools report `Saved to: <path>` (per-record: one path per line); storage de-duplicates names, so tests never guess a file name. On the docker channel `/data/...` maps to the bind-mounted storage folder.
3. **Generated documents are read back** (`OutputText`) and checked for values from the data — existence alone proves nothing.
4. **Failures are asserted strictly**: `isError: true` **and** the tool's documented prefix (`Document assembly failed for '<file>'`, …), plus the storage listing is unchanged.
5. **Unlicensed by design.** Evaluation mode adds a line and a worksheet and replaces e-mail HTML bodies; assertions check data values only where evaluation keeps them, and the one licence-dependent check branches on `get_license_status`.
6. **Images go in as base64**, never as a file path, so the same test works on the host and in the container.
7. **Missing fixtures fail, they do not skip.** A skipped test reported as a pass is how untested surface ships.

## House rules

1. **Changelog entries required** for any behaviour change (`changelog/NNN-slug.md`, schema in `changelog/README.md`), plus a CalVer bump in lockstep with the server repo.
2. **Version bumps flow through `Directory.Build.props`** and the defaults in both workflows.
3. **Never** add a `ProjectReference` to the server, hardcode tool names (use `ToolCatalog`), or commit licence files, keys, or fixtures of unclear provenance (`Files/README.md` must list every file).
4. **Never print secrets.** Metered keys are forwarded by name only (`docker -e NAME`), never on a command line.
5. Target framework is `net10.0` only — required by `dnx` and the MCP SDK.
