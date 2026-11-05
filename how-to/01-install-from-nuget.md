# Install from NuGet

Two install routes from [`nuget.org/packages/GroupDocs.Assembly.Mcp`](https://www.nuget.org/packages/GroupDocs.Assembly.Mcp):

1. **`dnx`** — run-on-demand (recommended for MCP clients). No install step.
2. **Global dotnet tool** — installed once, runs by name.

Both require the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

## Prerequisites

```bash
dotnet --version
# must print 10.0.x or higher
```

If this returns an older version or "command not found," install the .NET 10
SDK first. On Windows, verify `dnx` is on PATH:

```bash
dnx --help           # bash / Linux / macOS
dnx.cmd --help       # Windows (older shells)
```

`dnx` ships inside `.NET 10 SDK` at `C:\Program Files\dotnet\dnx.cmd` on
Windows and `~/.dotnet/dnx` on Linux/macOS.

## Option 1 — dnx (recommended)

```bash
dnx GroupDocs.Assembly.Mcp@26.10.0 --yes
```

The first invocation downloads the package into the NuGet cache; subsequent
invocations reuse it. `--yes` auto-confirms the package-trust prompt.

### Pinned vs always-latest

`@<version>` pins to that exact release. **Omit it to always pull the latest
stable** on each invocation:

```bash
dnx GroupDocs.Assembly.Mcp --yes                # latest stable, refreshed every run
dnx GroupDocs.Assembly.Mcp --prerelease --yes   # latest including pre-releases
```

| | Pinned (`@26.10.0`) | Unpinned |
|---|---|---|
| Use for | Client configs committed to repos, CI, shared team setups | Quick local smoke tests, dev machines that should track latest |
| Reproducibility | identical version on every machine / session | depends on when each machine first pulled |
| Behaviour on a new release | unaffected — keeps using cached version until you bump | downloads + uses the new version on next launch |
| Risk of unexpected breakage | low | a release that renames a tool / changes a schema will surprise you mid-session |
| Startup time on day-of-release | instant from cache | +1–10s for the version probe + download |

> **`dnx` does not support npm-style ranges** (`^26.4`, `~26.4.0`). It's pinned-exact
> or latest-stable — nothing in between. If you want a floor without bumping
> on every release, you'll need to update the pinned value manually.

The cache lives at `%USERPROFILE%\.nuget\packages\groupdocs.assembly.mcp\<version>\`
on Windows and `~/.nuget/packages/groupdocs.assembly.mcp/<version>/` on
Linux/macOS. Old versions accumulate there until you delete them.

**What you should see on stderr:**

```
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: ModelContextProtocol.Server.StdioServerTransport[...]
      Server (stream) (GroupDocs.Assembly.Mcp) transport reading messages.
warn: GroupDocs.Mcp.Core.Licensing.LicenseManager[0]
      No license configured. Running in evaluation mode. ...
```

stdout is reserved for JSON-RPC — the process is waiting for a client to speak
MCP on its stdin. Press `Ctrl+C` to exit.

### Smoke test with a raw JSON-RPC request

Pipe an `initialize` + `tools/list` sequence to see the advertised tools:

```bash
# bash
(
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}'
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
  sleep 2
) | GROUPDOCS_MCP_STORAGE_PATH=./docs dnx GroupDocs.Assembly.Mcp@26.10.0 --yes
```

You should see two JSON-RPC responses; the second lists the six advertised tools:
`assemble_document`, `assemble_per_record`, `get_template_fields`, `get_document_info`, `get_supported_formats` and `get_license_status`.

## Option 2 — global dotnet tool

```bash
dotnet tool install -g GroupDocs.Assembly.Mcp
groupdocs-assembly-mcp
```

To update:

```bash
dotnet tool update -g GroupDocs.Assembly.Mcp
```

To uninstall:

```bash
dotnet tool uninstall -g GroupDocs.Assembly.Mcp
```

The tool runs from `~/.dotnet/tools/groupdocs-assembly-mcp` (Linux/macOS) or
`%USERPROFILE%\.dotnet\tools\groupdocs-assembly-mcp.exe` (Windows). Make sure
that directory is on your `PATH` — the `dotnet tool install` output will warn
you if it isn't.

## Configuration

Set via environment variables when launching:

| Variable | Purpose | Default |
|---|---|---|
| `GROUPDOCS_MCP_STORAGE_PATH` | Folder the server reads templates and data from and writes generated documents to | current working directory |
| `GROUPDOCS_MCP_OUTPUT_PATH` | Optional — route output files to a different folder | same as storage |
| `GROUPDOCS_LICENSE_PATH` | Path to a GroupDocs licence file (`GroupDocs.Total.lic` or `GroupDocs.Assembly.lic`) — removes evaluation marks from generated documents | *(evaluation mode)* |

```bash
GROUPDOCS_MCP_STORAGE_PATH=/data/documents \
GROUPDOCS_LICENSE_PATH=/secrets/GroupDocs.Total.lic \
dnx GroupDocs.Assembly.Mcp@26.10.0 --yes
```

## Native prerequisites

Presentation templates (PPTX, PPT, ODP) are rendered through `System.Drawing`,
which needs **`libgdiplus`** on Linux and macOS. Saving a generated document to PDF
or XPS lays text out, so Linux also needs real fonts:

| Platform | Setup |
|---|---|
| Windows | Nothing. |
| Linux | `sudo apt-get install -y libgdiplus libfontconfig1 ttf-mscorefonts-installer` — `libgdiplus` for presentation templates, the fonts for PDF/XPS output |
| macOS | `brew install mono-libgdiplus` (only for presentation templates) |
| Docker | Nothing — the image already bundles `libgdiplus` and the fonts. |

Without `libgdiplus`, presentation templates fail with
`The type initializer for 'Gdip' threw an exception`. Without the fonts, PDF/XPS
output on Linux falls back to substitute fonts. The simplest zero-setup option on
Linux is the **Docker image**.

## License

Every tool works in evaluation mode without a license, with no row, page or
document-count limit. Documents generated by `assemble_document` and
`assemble_per_record` start with an `Evaluation Only. Created with GroupDocs.Assembly …`
line, saved workbooks gain an extra worksheet, and e-mail messages keep only their
plain-text body; the tool response then starts with an `Evaluation mode:` line.
`get_template_fields`, `get_document_info` and `get_supported_formats` only read
and are unaffected. `get_license_status` reports which mode the server is in.

Get a license from [purchase.groupdocs.com](https://purchase.groupdocs.com/).
Point `GROUPDOCS_LICENSE_PATH` at the `.lic` file, or set
`GROUPDOCS_METERED_PUBLIC_KEY` and `GROUPDOCS_METERED_PRIVATE_KEY` for metered
licensing, and the evaluation marks and the notice disappear.

## Verifying version at runtime

The server's `initialize` response includes `serverInfo.version`. With an MCP
client:

```text
initialize response → serverInfo: { name: "GroupDocs.Assembly.Mcp", version: "26.10.0" }
```

This value comes from the published assembly's `AssemblyInformationalVersion`
and is enforced to match the NuGet package version at build time — so it's
authoritative. If you want to script-check it, see
[06 — Integration tests](06-run-integration-tests.md) — the first test in
`ToolDiscoveryTests.cs` asserts this.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `dnx: command not found` | .NET 10 SDK not installed, or not on PATH | Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0). Ensure `C:\Program Files\dotnet\` (Windows) or equivalent is on PATH. |
| First run hangs for ~30 s | Package is downloading from nuget.org into cache | Normal. Subsequent runs are fast. |
| `No license configured. Running in evaluation mode.` | No licence configured | Expected. Generated documents carry evaluation marks; the read-only tools are unaffected. |
| Response starts with `Evaluation mode:` | Evaluation mode | Set `GROUPDOCS_LICENSE_PATH` to a valid `.lic` file, or the two metered keys. |
| PDF output uses substitute fonts (Linux) | Fonts not installed | `sudo apt-get install -y libfontconfig1 ttf-mscorefonts-installer` — see [Native prerequisites](#native-prerequisites). |
| `The type initializer for 'Gdip' threw an exception` on a PPTX/PPT/ODP template | `libgdiplus` not installed (Linux / macOS) | `sudo apt-get install -y libgdiplus` or `brew install mono-libgdiplus` — see [Native prerequisites](#native-prerequisites). |
| Client can't see any tools | MCP client didn't finish `initialize` handshake before issuing `tools/list` | Check your client config — most handle this automatically. If hand-rolling, always send `notifications/initialized` after `initialize`. |

## Next steps

- Wire into a client: [Claude Desktop](04-use-with-claude-desktop.md) · [VS Code / Copilot](05-use-with-vscode-copilot.md)
- Or try it in Docker: [02 — Run via Docker](02-run-via-docker.md)
- Verify the package's MCP registry listing: [03 — MCP registry](03-verify-mcp-registry.md)
