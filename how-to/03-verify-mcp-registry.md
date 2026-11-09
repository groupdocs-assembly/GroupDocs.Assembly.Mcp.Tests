# Verify on the MCP registry

The `GroupDocs.Assembly.Mcp` package is an **MCP server** (the csproj declares
`<PackageType>McpServer</PackageType>`). That makes it discoverable in two places:

1. **nuget.org** — filters by `packagetype=McpServer` and renders the
   package's embedded `server.json` as a ready-to-use `mcp.json` snippet.
2. **The MCP Registry** at [modelcontextprotocol.io](https://modelcontextprotocol.io)
   (once the server has been registered with the upstream registry).

This guide walks through verifying both.

## 1 — Verify on NuGet.org

### Direct link

[nuget.org/packages/GroupDocs.Assembly.Mcp](https://www.nuget.org/packages/GroupDocs.Assembly.Mcp)

### MCP filter listing

[nuget.org/packages?q=&packagetype=McpServer](https://www.nuget.org/packages?q=&packagetype=McpServer)

The package should appear in this list. If it doesn't:

- Check that the csproj still has `<PackageType>McpServer</PackageType>`
  (singular — the plural form is silently ignored by MSBuild).
- Allow up to ~1 hour after publish for nuget.org to re-index.

### Package page — generated `mcp.json` snippet

Scroll the package page until you see the **"MCP server"** card. It shows a
copy-pasteable snippet generated from the package's embedded
`.mcp/server.json`. The snippet should contain:

- `command: "dnx"`
- `args: ["GroupDocs.Assembly.Mcp@26.11.0", "--yes"]`
- `env` block with `GROUPDOCS_MCP_STORAGE_PATH`, `GROUPDOCS_MCP_OUTPUT_PATH`,
  `GROUPDOCS_LICENSE_PATH`.

If the snippet is missing or has wrong metadata, the embedded `server.json` is
the source of truth. You can inspect it directly by downloading the `.nupkg`:

```bash
curl -L -o pkg.nupkg \
  https://www.nuget.org/api/v2/package/GroupDocs.Assembly.Mcp/26.11.0
unzip -p pkg.nupkg .mcp/server.json | jq
```

Expected top-level fields:

```json
{
  "$schema": "https://static.modelcontextprotocol.io/schemas/2025-10-17/server.schema.json",
  "name": "io.github.groupdocs-assembly/groupdocs-assembly-mcp",
  "version": "26.11.0",
  "packages": [ { "registryType": "nuget", "identifier": "GroupDocs.Assembly.Mcp", ... } ],
  "repository": { "url": "https://github.com/groupdocs-assembly/GroupDocs.Assembly.Mcp" }
}
```

The `version` field MUST match the NuGet package version — the main repo's
`build.ps1` enforces this at pack time via `Assert-ServerJsonVersionMatchesDependencies`.

## 2 — Verify via MCP client discovery

Modern MCP clients with built-in server browsers (VS Code's Copilot MCP panel,
Claude Desktop's server discovery UI) pull from the registry and surface any
package with the `McpServer` type.

### VS Code

1. Open the Copilot chat panel.
2. Click the MCP servers icon / "Add MCP server".
3. Search for `groupdocs-assembly` or `assembly`.
4. The package should appear with the description from `server.json`:
   *"MCP server for GroupDocs.Assembly — generate documents from templates and data with AI agents."*

### Claude Desktop

Claude Desktop doesn't (yet) have built-in registry search, but you can confirm
discoverability indirectly:

- `dnx` successfully resolves `GroupDocs.Assembly.Mcp@26.11.0` from nuget.org.
- The snippet on the NuGet package page pastes directly into
  `claude_desktop_config.json` without edits (other than `storage_path`).

See [04 — Claude Desktop](04-use-with-claude-desktop.md) for config.

## 3 — Verify against the public MCP registry (if registered)

If the server has been registered with the [MCP Registry](https://registry.modelcontextprotocol.io) (separate opt-in step — the NuGet listing is automatic, the registry isn't), check:

```bash
curl -s https://registry.modelcontextprotocol.io/v0/servers \
  | jq '.[] | select(.name | contains("groupdocs-assembly"))'
```

If present, the response will include the same `server.json` content mirrored
from NuGet.

## Scripted verification

This repo's integration tests cover part of the above automatically:

- `ToolDiscoveryTests.ServerInfo_AdvertisesGroupDocsAssemblyMcp` — the server
  reports `serverInfo.name == "GroupDocs.Assembly.Mcp"` and a non-empty version
  that matches the package under test.
- `ToolDiscoveryTests.ListTools_ExposesEveryAssemblyToolAndLicenseStatus` — all
  five product tools plus `get_license_status` are advertised.
- `ToolDiscoveryTests.AllTools_HaveNonEmptyDescriptionAndObjectInputSchema` — every
  tool has a description and a valid input schema (which AI agents read).

Run them:

```bash
dotnet test -c Release --filter "FullyQualifiedName~ToolDiscovery"
```

See [06 — Integration tests](06-run-integration-tests.md) for detail.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Package doesn't appear in `packagetype=McpServer` filter | `<PackageType>` missing or typo (`PackageTypes` instead of `PackageType`) | Fix in the server repo's csproj. Re-publish. |
| `mcp.json` snippet on package page is blank | `.mcp/server.json` not packed | Verify the csproj `<None Include=".mcp\server.json" Pack="true" PackagePath=".mcp/server.json" />` entry exists and `server.json` is present. |
| Version in snippet mismatches package | `server.json` and `dependencies.props` drifted | In the server repo: update both, or let `build.ps1` fail the next pack. |
| MCP clients show the server but tool list is empty | Client connected but didn't finish `initialize` → `tools/list` sequence | See client logs. Most handle this automatically. |

## Next steps

- [01 — NuGet install](01-install-from-nuget.md) — once listed, confirm `dnx` resolution works
- [04 — Claude Desktop](04-use-with-claude-desktop.md) — paste the generated snippet
- [06 — Integration tests](06-run-integration-tests.md) — automate these checks
