# Use with Claude Desktop

Connect the MCP server to Claude Desktop (macOS / Windows) so you can ask
Claude to generate documents from your templates and data — fill a Word, Excel,
PowerPoint or e-mail template with JSON, XML, CSV or a spreadsheet table, save the result
as PDF, and produce one document per record for a mail merge.

## Prerequisites

- [Claude Desktop](https://claude.ai/download) installed and logged in.
- One of:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for the `dnx` route — recommended), or
  - [Docker](https://www.docker.com/products/docker-desktop) (for the container route).

## Config file location

| OS | Path |
|---|---|
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |

Create the file if it doesn't exist.

## Option A — dnx (recommended)

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Assembly.Mcp@26.10.0", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

- Replace `/Users/you/Documents` with an **absolute path** to the folder
  containing the templates and data you want Claude to work with. Generated
  documents are written there too.
- On Windows use `"C:\\Users\\you\\Documents"` (double-escaped backslashes) or
  forward slashes: `"C:/Users/you/Documents"`.

Full example: [examples/claude-desktop.json](../examples/claude-desktop.json).

### If Claude can't find `dnx`

Claude Desktop launches child processes with a minimal PATH — `dnx` may not be
found on macOS even though it works in your shell. Use the absolute path:

```json
"command": "/usr/local/share/dotnet/dnx"
```

On Windows:

```json
"command": "C:\\Program Files\\dotnet\\dnx.cmd"
```

Find the correct path with:

```bash
which dnx            # macOS / Linux
where dnx.cmd        # Windows (from cmd)
```

## Option B — Docker

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/you/Documents:/data",
        "ghcr.io/groupdocs-assembly/assembly-net-mcp:26.10.0"
      ]
    }
  }
}
```

This works even if you don't have the .NET SDK installed. The first invocation
pulls the image; subsequent launches are fast.

## Option C — Global dotnet tool

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "type": "stdio",
      "command": "groupdocs-assembly-mcp",
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

Requires you've already run `dotnet tool install -g GroupDocs.Assembly.Mcp`
(see [01 — NuGet install](01-install-from-nuget.md)).

## Restart Claude Desktop

After editing the config, fully quit and reopen Claude Desktop. On macOS,
`Cmd+Q` — closing the window isn't enough.

## Verify the connection

1. Open a new conversation.
2. Click the **🔨 tools** icon in the composer — you should see
   `assemble_document`, `assemble_per_record`, `get_template_fields` and the other Assembly
   tools listed under `groupdocs-assembly`.
3. If the icon shows an error badge, hover for the details. The most common
   issue is a bad `command` path or invalid `GROUPDOCS_MCP_STORAGE_PATH`.

## Example prompts

```
Fill invoice-template.docx with this order and save it as a PDF: {"customer":{"Name":"Jane Doe"},"items":[{"Name":"Widget","Price":9.5}]}

What data does contract-template.docx need? Give me a JSON skeleton.

Generate a welcome letter for every customer in customers.json, named after the customer.

Build the report from contracts-table.xlsx and Contracts.xlsx.

Which formats can GroupDocs.Assembly fill and save?
```

Claude will pick the right tool — `get_template_fields` to learn what data a template
needs, `assemble_document` to fill it, `assemble_per_record` for one document per record,
and `get_document_info` to inspect a template or a data file — and compose its answer from
the tool results.

## License note

Every tool works in evaluation mode without a license. Generated documents start with
an `Evaluation Only. Created with GroupDocs.Assembly …` line, and the response then starts
with `Evaluation mode:`. The read-only tools are unaffected. Add the license path to remove the marks:

```json
"env": {
  "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents",
  "GROUPDOCS_LICENSE_PATH": "/Users/you/.secrets/GroupDocs.Total.lic"
}
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| Server not listed in tools icon | Config JSON has a typo — Claude silently drops unparseable entries. Run it through `jq . claude_desktop_config.json`. |
| Server listed but greyed out | Claude couldn't launch the process. Check `~/Library/Logs/Claude/mcp*.log` on macOS or `%APPDATA%\Claude\logs\mcp*.log` on Windows for stderr from the server. |
| "No license configured" warnings | Expected in evaluation mode. All tools still work; generated documents carry evaluation marks. |
| Response starts with `Evaluation mode:` | Evaluation mode. Set `GROUPDOCS_LICENSE_PATH` to a valid `.lic` file. |

## Next steps

- [05 — Use with VS Code / Copilot](05-use-with-vscode-copilot.md)
- [03 — MCP registry](03-verify-mcp-registry.md) — confirm the snippet matches what's on nuget.org
