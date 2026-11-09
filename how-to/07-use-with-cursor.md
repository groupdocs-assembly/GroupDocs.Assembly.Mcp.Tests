# Use with Cursor

Connect the MCP server to [Cursor](https://cursor.com) so you can ask its Agent
to generate documents locally from your templates and data — fill Word, Excel,
PowerPoint and e-mail templates with JSON, XML, CSV or spreadsheet data, and save the
result in the template's format or as PDF.

## Prerequisites

- Cursor installed and updated (MCP support is in **Settings → Tools & MCP**).
- One of:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for the `dnx` route — recommended), or
  - [Docker](https://www.docker.com/products/docker-desktop) (for the container route).

## Config file location

Cursor uses the **`mcpServers`** key (like Claude Desktop) — **not** `servers`
as in VS Code. Two scopes:

| Scope | Path |
|---|---|
| Global (all projects) | `~/.cursor/mcp.json` (macOS/Linux) · `%USERPROFILE%\.cursor\mcp.json` (Windows) |
| Project-only | `.cursor/mcp.json` in the workspace root |

Create the file if it doesn't exist.

## Option A — dnx (recommended)

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "command": "dnx",
      "args": ["GroupDocs.Assembly.Mcp@26.11.0", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

- Replace the storage path with an **absolute path** to the folder that holds your
  templates and data; generated documents are written there too. On Windows use `"C:\\Users\\you\\Documents"` (double-escaped) or
  forward slashes.
- Omit `@26.11.0` to always pull the latest stable.
- Add `"GROUPDOCS_LICENSE_PATH": "…/GroupDocs.Total.lic"` to `env` to remove
  evaluation marks from generated documents. Every tool still works in evaluation mode.

Copy-paste starter: [examples/cursor-mcp.json](../examples/cursor-mcp.json).

## Option B — Windows: full path to `dotnet.exe` (SSL / timeout workaround)

On Windows, Cursor launching `dnx` can fail with an **SSL / ~30 s timeout** on
the first package probe. Bypass `dnx` by running the already-cached tool DLL
directly with `dotnet.exe`:

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "command": "C:\\Program Files\\dotnet\\dotnet.exe",
      "args": [
        "C:\\Users\\you\\.nuget\\packages\\groupdocs.assembly.mcp\\26.11.0\\tools\\net10.0\\any\\GroupDocs.Assembly.Mcp.dll"
      ],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "C:\\Users\\you\\Documents"
      }
    }
  }
}
```

Populate the cache first by running `dnx GroupDocs.Assembly.Mcp@26.11.0 --yes` once
in a terminal, then point `args[0]` at the resulting
`…\.nuget\packages\groupdocs.assembly.mcp\<version>\tools\net10.0\any\GroupDocs.Assembly.Mcp.dll`.

## Option C — Docker

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/you/Documents:/data",
        "ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0"
      ]
    }
  }
}
```

## Reload and verify

1. Save `mcp.json`.
2. **Settings → Tools & MCP** → find `groupdocs-assembly` → toggle it on (or hit
   the reload icon). A green dot means it connected.
3. Expand it — you should see `assemble_document`, `assemble_per_record`,
   `get_template_fields` and the other Assembly tools.

## Example prompts (Agent mode)

```
Fill invoice-template.docx with this order and save it as a PDF: {"customer":{"Name":"Jane Doe"},"items":[{"Name":"Widget","Price":9.5}]}

What data does contract-template.docx need? Give me a JSON skeleton.

Generate a welcome letter for every customer in customers.json, named after the customer.

Build the report from contracts-table.xlsx and Contracts.xlsx.
```

The Agent will call `get_template_fields` / `assemble_document` / `assemble_per_record`
and compose its answer from the results.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Server greyed out / won't start on Windows | `dnx` SSL/timeout — use **Option B** (full `dotnet.exe` path + cached DLL). |
| Server not listed | JSON typo — Cursor silently drops unparseable entries. Validate with `jq . mcp.json`. Confirm the key is `mcpServers`, not `servers`. |
| Response starts with `Evaluation mode:` | Expected in evaluation mode. Add `GROUPDOCS_LICENSE_PATH` to `env` to remove it. |
| PDF output uses substitute fonts (Linux) | `sudo apt-get install -y libfontconfig1 ttf-mscorefonts-installer`, or use the Docker image. |
| Presentation template fails with `The type initializer for 'Gdip' threw an exception` | Install `libgdiplus` (`sudo apt-get install -y libgdiplus` / `brew install mono-libgdiplus`), or use the Docker image. |

## Next steps

- [04 — Use with Claude Desktop](04-use-with-claude-desktop.md)
- [05 — Use with VS Code / Copilot](05-use-with-vscode-copilot.md)
