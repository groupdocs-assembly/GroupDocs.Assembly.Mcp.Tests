# Use with VS Code / GitHub Copilot

VS Code's MCP support (used by GitHub Copilot agents and other MCP-aware
extensions) reads server definitions from `.vscode/mcp.json` in your workspace
or from the user-level MCP settings.

## Prerequisites

- VS Code (stable or Insiders) with a recent build that supports MCP.
- GitHub Copilot extension installed and signed in (optional — other MCP-aware
  extensions work too).
- One of: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0),
  Docker, or the global dotnet tool installed.

## Workspace config — `.vscode/mcp.json`

Create the file at the root of your workspace:

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "storage_path",
      "description": "Base folder for templates, data and generated documents.",
      "password": false
    }
  ],
  "servers": {
    "groupdocs-assembly": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Assembly.Mcp@26.11.0", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "${input:storage_path}"
      }
    }
  }
}
```

Full example: [examples/vscode-mcp.json](../examples/vscode-mcp.json).

The `inputs` block makes VS Code prompt for the storage path the first time
the server starts — handy for workspaces where the answer differs per machine
or contributor. For a fixed path, drop the `inputs` block and hardcode the env
value.

## Alternative configurations

### Docker launcher

```json
{
  "servers": {
    "groupdocs-assembly": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "${workspaceFolder}/documents:/data",
        "ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0"
      ]
    }
  }
}
```

`${workspaceFolder}` is substituted by VS Code at launch.

### Global dotnet tool launcher

```json
{
  "servers": {
    "groupdocs-assembly": {
      "type": "stdio",
      "command": "groupdocs-assembly-mcp",
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "${workspaceFolder}/documents"
      }
    }
  }
}
```

## Start the server

VS Code shows MCP servers in the Copilot chat panel. The first time after
editing `mcp.json`:

1. Reload the window: `Cmd/Ctrl+Shift+P` → **"Developer: Reload Window"**.
2. Open Copilot chat.
3. Click the tools / MCP icon — `groupdocs-assembly` should appear with
   `assemble_document`, `get_template_fields` and the other Assembly tools listed.
4. If you used `inputs`, VS Code prompts for `storage_path` on first use and
   caches the answer for the session.

## Discovery snippet from nuget.org

The [NuGet.org package page](https://www.nuget.org/packages/GroupDocs.Assembly.Mcp)
renders a ready-to-use snippet on its "MCP server" card. That snippet is
generated from the package's embedded `.mcp/server.json` and is functionally
identical to the config above — you can paste it directly into
`.vscode/mcp.json`.

If the NuGet-generated snippet diverges from this file, treat the NuGet one as
authoritative for the `command` / `args` / `env` names — it's what users will
copy into their editors.

## Example prompts for Copilot

```
Fill invoice-template.docx with this order and save it as a PDF: {"customer":{"Name":"Jane Doe"},"items":[{"Name":"Widget","Price":9.5}]}

What data does contract-template.docx need? Give me a JSON skeleton.

Generate a welcome letter for every customer in customers.json, named after the customer.

Use get_document_info on Contracts.xlsx — which tables and columns can a template use?
```

Copilot will negotiate the right tool calls. If you want deterministic tool
selection, reference the tool by name in your prompt
(`assemble_document`, `assemble_per_record`, `get_template_fields`, `get_document_info`, …).

## License configuration

Add to the `env` block:

```json
"env": {
  "GROUPDOCS_MCP_STORAGE_PATH": "${input:storage_path}",
  "GROUPDOCS_LICENSE_PATH": "/absolute/path/to/GroupDocs.Total.lic"
}
```

Without a license, every tool still works — generated documents just carry
evaluation marks (see
[01 — NuGet install § License](01-install-from-nuget.md#license)).

## Troubleshooting

| Symptom | Fix |
|---|---|
| MCP icon doesn't appear | Update VS Code to a build with MCP support; ensure the Copilot extension is signed in. |
| Server listed but greyed out | Launch failed. Open **Output** panel → select the MCP extension from the dropdown to see stderr from the server process. |
| `dnx: command not found` | VS Code's integrated PATH may not include the .NET 10 SDK. Use the absolute path in `command` — see [04 — Claude Desktop § If dnx can't be found](04-use-with-claude-desktop.md#if-claude-cant-find-dnx). |
| Storage prompt doesn't appear | `inputs` block missing or mistyped. VS Code silently drops invalid entries. |
| Tool calls return "file not found" | `storage_path` points at a folder that doesn't contain the templates or data files you're asking about. Remember — tools resolve by filename relative to storage, not by absolute path. |

## Next steps

- [04 — Claude Desktop](04-use-with-claude-desktop.md) — same server, different client
- [06 — Integration tests](06-run-integration-tests.md) — run the test suite locally
