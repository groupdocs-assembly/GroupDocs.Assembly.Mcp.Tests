---
id: 001
date: 2026-10-30
version: 26.11.0
type: feature
---

# Initial integration suite for GroupDocs.Assembly.Mcp

## What changed

- New repository, cloned in shape from `GroupDocs.Editor.Mcp.Tests` (26.10.0): the fixture with three
  launch channels — published NuGet via `dnx` (default), `MCP_SERVER_DLL` (a local build) and
  `MCP_SERVER_IMAGE` (a container, `--user` on Linux) — one server per test class, classes serialised,
  metered keys forwarded only to `MeteredServerFixture`.
- **Test classes** for every tool: `ToolDiscoveryTests` (6 tools, description and schema contract),
  `CoreContractTests`, `AssembleDocumentTests`, `AssemblePerRecordTests`, `GetTemplateFieldsTests`,
  `GetDocumentInfoTests`, `GetSupportedFormatsTests`, `ErrorHandlingTests`, and `MeteredLicensingTests`
  (`Category=Metered`). 64 tests outside the metered category.
- **Oracles read the output back** (`Fixtures/OutputText.cs`): DOCX/XLSX/PPTX parts, HTML, text, the PDF
  signature, quoted-printable e-mail bodies. A generated document must contain values from its data.
- **Fixtures** in `Files/`: eleven templates and five data files from the upstream
  GroupDocs.Assembly-for-.NET examples (MIT, 0.2 MB), provenance in `Files/README.md`; five synthetic
  templates and a records file generated at start-up. Images are passed as base64, so the same tests run
  on the host and in the container.
- **CI**: `integration.yml` runs a dnx job on Linux, Windows and macOS (macOS non-blocking) and a docker
  job against the GHCR image; `metered.yml` runs the metered category weekly or on demand. Linux installs
  `libgdiplus` for presentation templates and fonts for PDF/XPS output.
- How-to guides, examples and `docker-scripts/` adapted from the Editor suite.

## Why

GroupDocs.Assembly joins the MCP family as its 15th product. The suite is written against the tool
contract before the first release, with the checks the family's external audit found missing: the
Docker channel, strict failure oracles, outputs read back, and no silent success.

## Migration / impact

None — first release.
