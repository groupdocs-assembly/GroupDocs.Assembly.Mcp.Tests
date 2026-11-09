# GroupDocs.Assembly.Mcp.Tests

Integration tests for the [`GroupDocs.Assembly.Mcp`](https://www.nuget.org/packages/GroupDocs.Assembly.Mcp)
NuGet package — an MCP server that lets AI agents generate Word, Excel, PowerPoint, PDF and e-mail
documents from templates and data, locally, with [GroupDocs.Assembly](https://products.groupdocs.com/assembly).

This repository validates the **published** artifact end-to-end: it launches the server via `dnx`
(or the Docker image), connects as an MCP client, and exercises every advertised tool against real
templates and data. It also doubles as a copy-pasteable set of example configs and how-to guides for
every deployment channel.

## Documentation

- [how-to/](how-to/) — step-by-step guides for every deployment channel
  ([NuGet](how-to/01-install-from-nuget.md),
  [Docker](how-to/02-run-via-docker.md),
  [MCP registry](how-to/03-verify-mcp-registry.md),
  [Claude Desktop](how-to/04-use-with-claude-desktop.md),
  [VS Code / Copilot](how-to/05-use-with-vscode-copilot.md),
  [running the tests](how-to/06-run-integration-tests.md),
  [Cursor](how-to/07-use-with-cursor.md)).
- [examples/](examples/) — ready-to-paste `claude-desktop.json`, `vscode-mcp.json`,
  `cursor-mcp.json` and `docker-compose.yml`.
- [Files/](Files/) — the real fixture templates and data, and where each one came from.
- [AGENTS.md](AGENTS.md) — orientation for AI coding agents working in this repo.
- [llms.txt](llms.txt) — machine-readable summary for LLM tooling.
- [changelog/](changelog/) — one entry per change set.

## What gets tested

| Area | Covered by |
|---|---|
| Package installs and starts (dnx, local DLL, or Docker image) | [McpServerFixture](src/GroupDocs.Assembly.Mcp.Tests/Fixtures/McpServerFixture.cs) |
| Handshake, server info, the 6-tool list, description and schema contract | [ToolDiscoveryTests](src/GroupDocs.Assembly.Mcp.Tests/ToolDiscoveryTests.cs) |
| Shared input/error contract: `fileName` form, base64 template, missing-file listing, `isError`, `get_license_status` | [CoreContractTests](src/GroupDocs.Assembly.Mcp.Tests/CoreContractTests.cs) |
| `assemble_document` — JSON array/object, XML, CSV, spreadsheet table; Word, text, Markdown, HTML, workbook, presentation and e-mail templates; PDF, DOCX and HTML output with its image folder; options | [AssembleDocumentTests](src/GroupDocs.Assembly.Mcp.Tests/AssembleDocumentTests.cs) |
| `assemble_per_record` — one file per record, naming, `recordsPath`, partial failure, `maxDocuments` | [AssemblePerRecordTests](src/GroupDocs.Assembly.Mcp.Tests/AssemblePerRecordTests.cs) |
| `get_template_fields` — names, fields, skeleton; tags spanning paragraphs; binary templates; `{{…}}`; skeleton round trip | [GetTemplateFieldsTests](src/GroupDocs.Assembly.Mcp.Tests/GetTemplateFieldsTests.cs) |
| `get_document_info` — a file as a template and as data (JSON, CSV, document tables) | [GetDocumentInfoTests](src/GroupDocs.Assembly.Mcp.Tests/GetDocumentInfoTests.cs) |
| `get_supported_formats` — the family catalog, data formats, and save targets for one template | [GetSupportedFormatsTests](src/GroupDocs.Assembly.Mcp.Tests/GetSupportedFormatsTests.cs) |
| Corrupted template, missing field, unclosed block, wrong target, bad data — failure prefix, hint, nothing written | [ErrorHandlingTests](src/GroupDocs.Assembly.Mcp.Tests/ErrorHandlingTests.cs) |
| Metered licensing engages; keys never echoed (costs money — separate workflow) | [MeteredLicensingTests](src/GroupDocs.Assembly.Mcp.Tests/MeteredLicensingTests.cs) |

Every generated document is read back and checked for values from the data, so a test proves the
assembly happened rather than merely that a file appeared.

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet test --filter "Category!=Metered"
```

Test a specific published version:

```bash
dotnet test -p:McpPackageVersion=26.11.0 --filter "Category!=Metered"
# or
MCP_PACKAGE_VERSION=26.11.0 dotnet test --filter "Category!=Metered"
```

Test a local build of the server before it is published, or a container image:

```bash
MCP_SERVER_DLL=/path/to/GroupDocs.Assembly.Mcp.dll dotnet test --filter "Category!=Metered"
MCP_SERVER_IMAGE=ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0 dotnet test --filter "Category!=Metered"
```

The suite runs **without a licence** by design. Set `GROUPDOCS_LICENSE_PATH` to remove evaluation
marks from outputs; the assertions hold in both modes (the one e-mail check that evaluation mode
would hide branches on `get_license_status`).

On Linux, presentation templates need `libgdiplus` (`sudo apt-get install -y libgdiplus`).

## CI

[.github/workflows/integration.yml](.github/workflows/integration.yml) runs on every push / PR, nightly,
on `workflow_dispatch` (with `package_version` and `image` inputs), and on `repository_dispatch`
(`nuget-published`) fired by the server repo's publish pipeline.

- **dnx job** — `ubuntu-latest`, `windows-latest`, `macos-latest`. Linux and Windows gate the release;
  macOS is non-blocking while the family-wide macOS runner hang is open. Linux installs `libgdiplus`
  (presentation templates) and `libfontconfig1` + `ttf-mscorefonts-installer` (PDF/XPS output).
- **docker job** — the same suite against the GHCR image on Linux, with `--user` so the container can
  write into the bind mount.

[.github/workflows/metered.yml](.github/workflows/metered.yml) runs the `Metered` category weekly or on
demand. It bills a real metered account, so it is never on push.

## Release smoke hook

The server repo's publish workflow dispatches `nuget-published` with
`{"package_version": "x.y.z"}` after the NuGet push, so every release is tested against live nuget.org.
Re-dispatch manually with an explicit `package_version` if a push-triggered run started before NuGet
finished indexing.

## License

MIT — see [LICENSE](LICENSE).
