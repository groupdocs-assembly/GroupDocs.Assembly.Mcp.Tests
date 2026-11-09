# Docker Scripts — Integration Test Automation

Complete bash script suite for running GroupDocs.Assembly.Mcp integration tests in both local and Docker environments. **Works on Windows (Git Bash/WSL), Linux, and macOS.**

## 📋 Overview

This folder contains production-ready bash scripts for testing the GroupDocs.Assembly.Mcp NuGet package across all scenarios:

- **ToolDiscovery** — MCP server handshake and tool listing
- **CoreContract** — file resolution, the missing-file listing, `get_license_status`
- **AssembleDocument** — JSON, XML, CSV and table data into Word, Excel, PowerPoint, e-mail, Markdown and HTML templates; PDF output
- **AssemblePerRecord** — mail merge: one document per record, named by number or by a record field
- **GetTemplateFields** — a template's tags, data names, fields and JSON skeleton
- **GetDocumentInfo / GetSupportedFormats** — a file as a template and as data; the format catalog
- **ErrorHandling** — missing fields, unclosed tags, bad or doubled data, corrupted templates
- **MeteredLicensing** (`Category=Metered`) — needs the metered keys and bills real credit

## 📁 Files

Numeric prefixes indicate the recommended running sequence.

| File | Purpose |
|------|---------|
| `00_quick-start.sh` | **Cheatsheet** — Copy-paste reference. Not meant to be executed directly. |
| `01_verify-setup.sh` | **Preflight** — Check Docker, .NET SDK, project structure, connectivity. |
| `02_test-all-scenarios.sh` | **Main runner** — Runs the integration suite locally against the published NuGet (via `dnx`). |
| `03_test-docker-compose.sh` | **SDK-container runner** — Runs the suite inside a `dotnet/sdk` container; still uses `dnx` to spawn the MCP server. |
| `04_run-server-with-samples.sh` | **Interactive Docker MCP server** — Launches the published `assembly-net-mcp` container with `Files/` mounted, for ad-hoc smoke testing. |
| `helpers.sh` | Library — shared logging, Docker, .NET utilities (sourced, not run). |
| `README.md` | This file. |

## 🚀 Quick Start

### Prerequisites

**Windows:**
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (includes Docker CLI)
- [Git Bash](https://git-scm.com/download/win) or [WSL2](https://learn.microsoft.com/en-us/windows/wsl/install)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for local testing)

**Linux:**
```bash
sudo apt-get install docker.io dotnet-sdk-10.0 git
```

**macOS:**
```bash
brew install docker dotnet git
# Also install Docker Desktop or Docker for Mac
```

### Basic Usage

```bash
# Run all tests against the LATEST nuget.org release (fastest — local execution, default)
./02_test-all-scenarios.sh

# Run tests in Docker containers (also defaults to latest)
./03_test-docker-compose.sh

# Try the published Docker MCP image with the repo's Files mounted
./04_run-server-with-samples.sh

# Run specific test scenario
./02_test-all-scenarios.sh --filter AssembleDocumentTests

# Pin to a specific package version (reproducible / CI runs)
./02_test-all-scenarios.sh --version 26.11.0

# Use license for licensed-mode tests
./02_test-all-scenarios.sh --license /path/to/GroupDocs.Total.lic
```

## 📖 Detailed Usage

### `02_test-all-scenarios.sh` — Local Test Runner

**Best for:** Local development, quick validation, CI/CD pipelines

```bash
Usage:
  ./02_test-all-scenarios.sh [OPTIONS]

Options:
  --version VERSION       Test specific package version (default: latest)
                          Use "latest" or omit to track nuget.org's most recent
                          stable release. Pin (e.g. "26.11.0") for reproducible
                          / shared / CI runs.
  --filter PATTERN        Run only tests matching pattern
  --no-build              Skip local .NET build, use pre-built
  --license PATH          Path to GroupDocs license file
  --help                  Show help message

Examples:
  # All tests against latest stable (default)
  ./02_test-all-scenarios.sh

  # Only AssembleDocument tests
  ./02_test-all-scenarios.sh --filter AssembleDocumentTests

  # Pin to 26.11.0 with custom license
  ./02_test-all-scenarios.sh --version 26.11.0 --license /path/to/lic

  # Skip rebuild (use cached binaries)
  ./02_test-all-scenarios.sh --no-build --filter ToolDiscovery
```

**What it does:**
1. ✅ Validates Docker, .NET SDK, and project structure
2. ✅ Builds the test project in Release mode
3. ✅ Runs all integration tests with specified filters
4. ✅ Reports results in console + JSON format

**Output:**
```
Passed  ToolDiscoveryTests.ServerInfo_AdvertisesGroupDocsAssemblyMcp
Passed  AssembleDocumentTests.XmlData_SavedAsPdf
Passed  CoreContractTests.AssembleDocument_MissingTemplate_ReturnsTheAvailableFilesListing
...
✓ All integration test scenarios completed!
```

---

### `03_test-docker-compose.sh` — Containerized Test Runner

**Best for:** CI/CD, isolated environments, cross-platform reproducibility

```bash
Usage:
  ./03_test-docker-compose.sh [OPTIONS]

Options:
  --version VERSION       Test specific package version (default: latest)
                          Use "latest" or omit to track nuget.org's most recent
                          stable release. Pin (e.g. "26.11.0") for reproducible
                          / shared / CI runs.
  --filter PATTERN        Run only tests matching pattern
  --license PATH          Path to GroupDocs license file
  --keep                  Keep containers running (for debugging)
  --help                  Show help message

Examples:
  # Run all tests in Docker against latest stable (default)
  ./03_test-docker-compose.sh

  # Pin to a specific version
  ./03_test-docker-compose.sh --version 26.11.0

  # Keep containers for inspection
  ./03_test-docker-compose.sh --keep
```

**What it does:**
1. ✅ Generates isolated docker-compose.yml
2. ✅ Mounts Files volume (read-only)
3. ✅ Runs tests in .NET SDK 10.0 container
4. ✅ Auto-cleans resources (unless `--keep` is set)

**Generated docker-compose file structure:**
```yaml
services:
  assembly-server:
    image: ghcr.io/groupdocs-assembly/assembly-net-mcp:latest
    volumes:
      - files-volume:/data:ro

  test-runner:
    image: mcr.microsoft.com/dotnet/sdk:10.0-alpine
    volumes:
      - workspace-volume:/workspace:ro
      - files-volume:/data:ro
    depends_on:
      - assembly-server
```

---

## 📂 Sample Documents

The `Files/` folder contains test fixtures:

```
Files/
  ├── managers.docx         (Word template: managers and their contracts)
  ├── contracts-table.xlsx  (workbook template; also .docx / .pptx / .msg)
  ├── Managers.json         (data: JSON)
  ├── Managers.xml          (data: XML)
  ├── Persons.csv           (data: CSV)
  ├── Contracts.xlsx        (data: a workbook table)
  ├── Customers.json        (data: records for per-record assembly)
  └── ...
```

**Files/ is required.** It holds the real fixtures every test class uses (provenance in
`Files/README.md`); a missing fixture fails the run rather than skipping tests.

**To add your own:**
```bash
cp my-template.docx ../Files/   # then add it to SampleDocuments.RealSamples and Files/README.md
./02_test-all-scenarios.sh
```

---

## 🔐 License Files

### Using a License

```bash
# Run with a license (generated documents carry no evaluation marks)
export GROUPDOCS_LICENSE_PATH=/path/to/GroupDocs.Total.lic
./02_test-all-scenarios.sh

# Or inline
./02_test-all-scenarios.sh --license /path/to/GroupDocs.Total.lic
```

### Evaluation Mode (Default)

Without a license every tool works and every test runs; nothing is skipped:
- `AssembleDocumentTests.UnlicensedRuns_LeadWithTheEvaluationNotice` — ✅ expects the `Evaluation mode:` line (and its absence when licensed)
- `AssembleDocumentTests.EmailTemplate_FillsSubjectAndBody` — ✅ checks the HTML body only when licensed (evaluation mode keeps only the plain-text body)
- `MeteredLicensingTests` — need `GROUPDOCS_METERED_PUBLIC_KEY` / `GROUPDOCS_METERED_PRIVATE_KEY`; exclude them with `Category!=Metered` when the keys are not set

License file must be readable by the process running the tests.

---

## 🎯 Test Scenarios & Filters

Run individual test suites with `--filter`:

| Filter | Tests | Command |
|--------|-------|---------|
| `ToolDiscovery` | 6 tests | `./02_test-all-scenarios.sh --filter ToolDiscovery` |
| `CoreContract` | 7 tests | `./02_test-all-scenarios.sh --filter CoreContract` |
| `AssembleDocumentTests` | 16 tests | `./02_test-all-scenarios.sh --filter AssembleDocumentTests` |
| `AssemblePerRecord` | 8 tests | `./02_test-all-scenarios.sh --filter AssemblePerRecord` |
| `GetTemplateFields` | 6 tests | `./02_test-all-scenarios.sh --filter GetTemplateFields` |
| `GetDocumentInfo` | 7 tests | `./02_test-all-scenarios.sh --filter GetDocumentInfo` |
| `GetSupportedFormats` | 2 tests | `./02_test-all-scenarios.sh --filter GetSupportedFormats` |
| `ErrorHandling` | 12 tests | `./02_test-all-scenarios.sh --filter ErrorHandling` |
| (default) | 64 tests + 3 metered | `./02_test-all-scenarios.sh` |

**Example: Quick validation (only discovery)**
```bash
./02_test-all-scenarios.sh --filter ToolDiscovery  # ~2 seconds
```

---

## 🐳 Docker Configuration

### Using Latest Package

```bash
./03_test-docker-compose.sh --version latest
```

### Building Docker Images Locally

```bash
# Build from server source (requires main repo)
docker build -t assembly-net-mcp:local ../../../GroupDocs.Assembly.Mcp/

# Run tests against local image
./03_test-docker-compose.sh
```

### Debugging Docker Tests

```bash
# Keep containers running
./03_test-docker-compose.sh --keep

# View logs
docker logs <container-id>

# Inspect running containers
docker ps

# Clean up manually
docker compose -f docker-compose.test.yml down -v
```

---

## 🔧 Advanced Usage

### Test Multiple Versions

```bash
for version in 26.11.0 latest; do
  echo "Testing version $version..."
  ./02_test-all-scenarios.sh --version $version || exit 1
done
```

### Parallel Testing (Different Filters)

```bash
# Run in separate terminals
./02_test-all-scenarios.sh --filter ToolDiscovery &
./02_test-all-scenarios.sh --filter AssembleDocumentTests &
./02_test-all-scenarios.sh --filter ErrorHandling &
wait
```

### CI/CD Integration

**GitHub Actions:**
```yaml
- name: Run integration tests
  run: |
    cd docker-scripts
    ./02_test-all-scenarios.sh --version ${{ matrix.version }}
  env:
    GROUPDOCS_LICENSE_PATH: ${{ secrets.GROUPDOCS_LICENSE }}
```

**Azure Pipelines:**
```yaml
- script: |
    cd docker-scripts
    chmod +x 02_test-all-scenarios.sh
    ./02_test-all-scenarios.sh
  displayName: 'Run Integration Tests'
```

---

## 📋 Helper Functions

The `helpers.sh` file provides reusable bash functions for scripting:

```bash
#!/bin/bash
source "$(dirname "$0")/helpers.sh"

# Logging
log_info "Starting..."
log_success "Completed!"
log_error "Failed"
log_warning "Be careful"

# Checks
check_command docker
check_dotnet_sdk 10
check_file_exists /path/to/file

# Docker utilities
docker_pull_image "ghcr.io/groupdocs-assembly/assembly-net-mcp:latest"
check_docker_daemon

# .NET utilities
dotnet_restore "/path/to/project"
dotnet_build "/path/to/project" "Release"
```

---

## 🐛 Troubleshooting

### "Docker daemon is not running"
```bash
# Windows/macOS: Start Docker Desktop
# Linux
sudo systemctl start docker
```

### "Permission denied" on .sh files (Windows Git Bash)
```bash
git update-index --chmod=+x 02_test-all-scenarios.sh
./02_test-all-scenarios.sh
```

### "dotnet command not found"
```bash
# Install .NET 10 SDK
# https://dotnet.microsoft.com/download/dotnet/10.0

# Or skip build
./02_test-all-scenarios.sh --no-build
```

### Tests timeout in Docker
```bash
# Increase Docker Desktop memory: Preferences → Resources → Memory (8GB+)
# Or use local testing instead
./02_test-all-scenarios.sh
```

### Run still reports evaluation mode with a license
```bash
# Verify path is absolute
ls -la /path/to/GroupDocs.Total.lic

# Set environment variable
export GROUPDOCS_LICENSE_PATH=$(pwd)/license/GroupDocs.Total.lic
./02_test-all-scenarios.sh
```

---

## 📊 Expected Output

### All Tests Pass
```
Passed  ToolDiscoveryTests.ServerInfo_AdvertisesGroupDocsAssemblyMcp
Passed  ToolDiscoveryTests.ListTools_ExposesEveryAssemblyToolAndLicenseStatus
Passed  ToolDiscoveryTests.AllTools_HaveNonEmptyDescriptionAndObjectInputSchema
Passed  AssembleDocumentTests.JsonArray_FillsNestedLists_AndIsNamedFromTheTemplate
Passed  AssembleDocumentTests.PresentationTemplate_SavedAsPdf
Passed  AssemblePerRecordTests.OneDocumentPerRecord_NamedAfterAField
Passed  GetTemplateFieldsTests.NestedTemplate_ListsNamesFieldsAndASkeleton
Passed  GetDocumentInfoTests.Workbook_AsData_ListsItsTableColumns
Passed  GetSupportedFormatsTests.Catalog_ListsFourFamiliesAndTheDataFormats
Passed  ErrorHandlingTests.AssembleDocument_CorruptedTemplate_ReportsFailureAndServerStaysUp
...

Total: 64, Passed: 64

✓ All integration test scenarios completed!
```

### Files Detected
```
✓ Found 5 sample documents
  /workspace/Files/managers.docx
  /workspace/Files/Managers.json
  ...
```

---

## 📝 Notes

- Scripts are **cross-platform** (Windows/Linux/macOS)
- All paths are **automatically normalized** for your OS
- Docker volumes are **read-only** for safety
- Tests can run **locally or containerized**
- License path is **optional** (evaluation mode default)
- Results are logged in **JSON format** for CI integration

---

## 📚 Related Files

- [GroupDocs.Assembly.Mcp NuGet](https://www.nuget.org/packages/GroupDocs.Assembly.Mcp)
- [Integration Tests Guide](../how-to/06-run-integration-tests.md)
- [Docker Setup Guide](../how-to/02-run-via-docker.md)
- [Test Project](../src/GroupDocs.Assembly.Mcp.Tests/GroupDocs.Assembly.Mcp.Tests.csproj)

---

## 💡 Tips & Best Practices

1. **For quick validation:** Use `--filter ToolDiscovery` (2-5 seconds)
2. **For CI/CD:** Use `02_test-all-scenarios.sh` (local is faster than Docker)
3. **For debugging:** Use `03_test-docker-compose.sh --keep` to inspect containers
4. **For multiple versions:** Use a loop with `--version` flag
5. **For sample docs:** Drop files in `Files/`, they're auto-mounted
6. **For licenses:** Store in a secure location, pass via env var in CI

---

**Last Updated:** 2026-04-23  
**Minimum Requirements:** Bash 4.0+, Docker 20.10+, .NET 10 SDK
