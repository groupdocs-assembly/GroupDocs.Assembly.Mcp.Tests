# Run the integration tests

This repo's test suite validates the **published** `GroupDocs.Assembly.Mcp`
NuGet package end-to-end — it spawns the server via `dnx`, connects as an MCP
client, and exercises every advertised tool. Useful when you want to confirm a
release is healthy before promoting it, or gate CI on live smoke checks.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Network access to nuget.org (the first run downloads the package)
- Optional: a GroupDocs license file to run the licensed-mode assertions
- Linux / macOS: `libgdiplus` for the presentation-template tests (see
  [01 — Native prerequisites](01-install-from-nuget.md#native-prerequisites))

## Run locally

```bash
# All 64 non-metered tests against the default pinned version (26.11.0)
dotnet test -c Release --filter "Category!=Metered"
```

## Run against a different published version

```bash
# Via MSBuild property
dotnet test -c Release -p:McpPackageVersion=26.11.0

# Or via env var
MCP_PACKAGE_VERSION=26.11.0 dotnet test -c Release
```

Version resolution order (highest wins):

1. `MCP_PACKAGE_VERSION` environment variable
2. `McpPackageVersion` MSBuild property → baked into assembly metadata
3. Default: `26.11.0`

## Unlock licensed-mode tests

Every test runs with or without a licence; a few assert the mode they find.
`AssembleDocumentTests.UnlicensedRuns_LeadWithTheEvaluationNotice` expects the
`Evaluation mode:` line only when unlicensed, and the e-mail template test checks
the HTML body only when licensed (evaluation mode keeps only the plain-text body).

```bash
export GROUPDOCS_LICENSE_PATH=/absolute/path/to/GroupDocs.Total.lic
dotnet test -c Release --filter "Category!=Metered"
```

The license path is forwarded into the server child process by
`McpServerFixture`.

`MeteredLicensingTests` (`Category=Metered`) spend real metered credit, so the commands
above exclude them, as CI does. Set `GROUPDOCS_METERED_PUBLIC_KEY` and
`GROUPDOCS_METERED_PRIVATE_KEY`, then run them on purpose:

```bash
dotnet test -c Release --filter "Category=Metered"
```

## Run a subset

```bash
# Only discovery (fastest — no tool invocations after handshake)
dotnet test -c Release --filter "FullyQualifiedName~ToolDiscovery"

# Only assemble_document tests
dotnet test -c Release --filter "FullyQualifiedName~AssembleDocumentTests"

# Only error-handling tests
dotnet test -c Release --filter "FullyQualifiedName~ErrorHandling"
```

## Expected output

```
Passed  ToolDiscoveryTests.ServerInfo_AdvertisesGroupDocsAssemblyMcp
Passed  ToolDiscoveryTests.ListTools_ExposesEveryAssemblyToolAndLicenseStatus
Passed  CoreContractTests.AssembleDocument_MissingTemplate_ReturnsTheAvailableFilesListing
Passed  AssembleDocumentTests.XmlData_SavedAsPdf
Passed  AssembleDocumentTests.SpreadsheetTable_IntoAWorkbookTemplate
Passed  AssembleDocumentTests.PresentationTemplate_SavedAsPdf
Passed  AssemblePerRecordTests.OneDocumentPerRecord_NamedAfterAField
Passed  GetTemplateFieldsTests.Skeleton_FilledIn_AssemblesTheTemplate
Passed  GetDocumentInfoTests.Workbook_AsData_ListsItsTableColumns
Passed  GetSupportedFormatsTests.Catalog_ListsFourFamiliesAndTheDataFormats
Passed  ErrorHandlingTests.UnclosedForeach_SaysWhichTagIsMissing
…

Total: 64, Passed: 64
```

The first test run is slower (~60s) because `dnx` downloads the package into
the NuGet cache.

## Add real-world fixtures

Real fixtures live in [Files/](../Files/), with their provenance in
[Files/README.md](../Files/README.md). To add one:

1. Drop the file into `Files/`. The csproj's
   `<None Include="..\..\Files\**\*" CopyToOutputDirectory="PreserveNewest" />`
   glob copies it to the test output, which `McpServerFixture` seeds into the
   server's storage path.
2. Add its name to `SampleDocuments.RealSamples`, plus a constant for any value a
   test relies on — and check the value really is in the template or data file.
3. Add a row to `Files/README.md` (upstream source, tools exercised) and keep the
   folder under about 5 MB. Only add files you are allowed to redistribute.

## Use in CI

The workflow at [.github/workflows/integration.yml](../.github/workflows/integration.yml)
runs on four triggers:

- **`push` + `pull_request`** — validates repo changes.
- **Nightly cron** (`0 6 * * *` UTC) — catches regressions in nuget.org, `dnx`,
  or the .NET runtime.
- **`workflow_dispatch`** with a `package_version` input — smoke-test any
  published version manually.
- **`repository_dispatch`** (`nuget-published` event) — fires from the main
  repo's publish pipeline after `dotnet nuget push`. Payload:
  `{ "package_version": "x.y.z" }`.

Matrix: `ubuntu-latest`, `windows-latest`, `macos-latest`.

### Wire the release-smoke hook in the server repo

Add this step to the server repo's publish workflow, right after the push step:

```yaml
- name: Dispatch integration tests
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  run: |
    gh api \
      repos/groupdocs-assembly/GroupDocs.Assembly.Mcp.Tests/dispatches \
      -f event_type=nuget-published \
      -f 'client_payload[package_version]=${{ steps.version.outputs.version }}'
```

The `GITHUB_TOKEN` scope is enough if both repos are in the same org.
Otherwise use a fine-grained PAT with `Contents: write` on the test repo.

### License secret in CI

Store a base64-encoded `.lic` file as the repo secret `GROUPDOCS_LICENSE`.
The workflow decodes it into `$RUNNER_TEMP` and exports `GROUPDOCS_LICENSE_PATH`
— the licensed-mode tests then run automatically.

```bash
# Locally: base64-encode and set the secret
base64 -w0 GroupDocs.Total.lic | gh secret set GROUPDOCS_LICENSE \
  --repo groupdocs-assembly/GroupDocs.Assembly.Mcp.Tests
```

## Debugging failures

### Inspect server stderr

`McpServerFixture` doesn't currently capture the child process's stderr — if a
test fails with a cryptic `"An error occurred invoking 'get_template_fields'"`,
reproduce the call manually:

```bash
mkdir -p /tmp/gd && cp some-template.docx /tmp/gd/
(
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"p","version":"1"}}}'
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_template_fields","arguments":{"template":{"filePath":"some-template.docx"}}}}'
  sleep 5
) | GROUPDOCS_MCP_STORAGE_PATH=/tmp/gd dnx GroupDocs.Assembly.Mcp@26.11.0 --yes \
    > stdout.log 2> stderr.log
tail -50 stderr.log
```

The server logs full exception stacks to stderr.

### Verbose test output

```bash
dotnet test -c Release --logger "console;verbosity=detailed"
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| `dnx: command not found` during test | Ensure .NET 10 SDK is installed. On Windows, `CommandResolver` looks for `dnx.cmd`; check it exists at `C:\Program Files\dotnet\dnx.cmd`. |
| `PresentationTemplate_*` tests fail with `The type initializer for 'Gdip' threw an exception` | `libgdiplus` missing on Linux / macOS. `sudo apt-get install -y libgdiplus` or `brew install mono-libgdiplus`. |
| `MeteredLicensingTests` fail with missing keys | They ran without the filter. Add `--filter "Category!=Metered"`, or set both metered keys to run them deliberately. |
| First run takes minutes | NuGet download. Subsequent runs hit the cache. |
| Cross-OS flakes in CI | Different line endings in Files/ fixtures. Commit with `text=auto` or binary mode in `.gitattributes`. |

## Next steps

- [03 — MCP registry](03-verify-mcp-registry.md) — cross-check registry state
- [01 — NuGet install](01-install-from-nuget.md) — manual smoke the same way users do
