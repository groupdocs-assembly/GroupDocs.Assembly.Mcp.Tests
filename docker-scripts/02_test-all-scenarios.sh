#!/bin/bash
################################################################################
# GroupDocs.Assembly.Mcp — Integration Test Runner (local)
#
# Runs the xUnit integration suite in-process on the host. The test fixtures
# themselves spawn the MCP server via `dnx` / NuGet, so Docker is NOT required
# here. For a containerized run, see ./03_test-docker-compose.sh.
#
# Scenarios:
#   - ToolDiscovery (server handshake, tool listing)
#   - CoreContract (file resolution, missing-file listing, license status)
#   - AssembleDocument (JSON / XML / CSV / table data into Word, Excel, PowerPoint,
#     e-mail, Markdown and HTML templates; PDF output; evaluation notice)
#   - AssemblePerRecord (mail merge: one document per record, file naming)
#   - GetTemplateFields (tags, data names, fields, JSON skeleton)
#   - GetDocumentInfo (a file as a template and as data)
#   - GetSupportedFormats (template formats, save targets per family, data formats)
#   - ErrorHandling (missing fields, unclosed tags, bad data, corrupted templates)
#   - MeteredLicensing (Category=Metered; needs the metered keys, bills real credit)
#
# Prerequisites:
#   - .NET 10 SDK
#   - Linux / macOS: libgdiplus for the presentation-template tests
#   - Files folder with test fixtures (optional)
#   - GROUPDOCS_LICENSE_PATH env var (optional, for licensed tests)
#
# Usage:
#   ./02_test-all-scenarios.sh [OPTIONS]
#
# Options:
#   --version VERSION       Test specific package version (default: latest)
#                           Use "latest" or omit the flag to track nuget.org's
#                           most recent stable release. Pin (e.g. "26.11.0") for
#                           reproducible / shared / CI runs.
#   --filter PATTERN        Run only tests matching pattern (e.g., AssembleDocumentTests)
#   --no-build              Skip local .NET build, use pre-built
#   --license PATH          Path to GroupDocs license file
#   --help                  Show this help message
#
# Examples:
#   ./02_test-all-scenarios.sh                                  # latest stable
#   ./02_test-all-scenarios.sh --version 26.11.0 --filter AssembleDocumentTests
#   ./02_test-all-scenarios.sh --license /path/to/GroupDocs.Total.lic
#
################################################################################

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'  # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_PROJECT_DIR="$PROJECT_ROOT/src/GroupDocs.Assembly.Mcp.Tests"
SAMPLE_DOCS_DIR="$PROJECT_ROOT/Files"
BUILD_CONFIG="Release"
MCP_PACKAGE_VERSION="${MCP_PACKAGE_VERSION:-latest}"
TEST_FILTER=""
SKIP_BUILD=false
LICENSE_PATH=""

################################################################################
# Functions
################################################################################

print_header() {
    echo -e "\n${BLUE}════════════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}════════════════════════════════════════════════════════════${NC}\n"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

show_help() {
    grep '^#' "$0" | grep -E "^\s*#\s+(Usage|Options|Examples|Prerequisites|GroupDocs)" | sed 's/^# //'
}

validate_environment() {
    print_header "Validating Environment"

    # Check .NET SDK (if not skipping build)
    if [ "$SKIP_BUILD" = false ]; then
        if ! command -v dotnet &> /dev/null; then
            print_error ".NET SDK is not installed. Install .NET 10 SDK"
            exit 1
        fi
        print_success "dotnet found: $(dotnet --version)"
    fi

    # Check project exists
    if [ ! -d "$TEST_PROJECT_DIR" ]; then
        print_error "Test project directory not found: $TEST_PROJECT_DIR"
        exit 1
    fi
    print_success "Test project found: $TEST_PROJECT_DIR"

    # Check Files
    if [ -d "$SAMPLE_DOCS_DIR" ]; then
        local doc_count=$(find "$SAMPLE_DOCS_DIR" -type f | wc -l)
        print_success "Found $doc_count sample documents in $SAMPLE_DOCS_DIR"
    else
        print_warning "Files directory not found. Creating empty directory."
        mkdir -p "$SAMPLE_DOCS_DIR"
    fi
}

build_project() {
    print_header "Building .NET Project"
    
    cd "$TEST_PROJECT_DIR"
    
    print_info "Restoring dependencies..."
    dotnet restore
    
    print_info "Building in $BUILD_CONFIG mode..."
    dotnet build -c "$BUILD_CONFIG"
    
    print_success "Build completed successfully"
    cd - > /dev/null
}

run_all_scenarios_locally() {
    print_header "Running Integration Tests Locally (Fast Mode)"
    
    cd "$TEST_PROJECT_DIR"
    
    print_info "Package version: $MCP_PACKAGE_VERSION"
    [ -n "$LICENSE_PATH" ] && export GROUPDOCS_LICENSE_PATH="$LICENSE_PATH"
    
    local test_args=("-c" "$BUILD_CONFIG" "-p:McpPackageVersion=$MCP_PACKAGE_VERSION")
    # Metered tests bill a real account and fail without keys; they run only from metered.yml.
    if [ -n "$TEST_FILTER" ]; then
        test_args+=("--filter" "Category!=Metered&FullyQualifiedName~$TEST_FILTER")
    else
        test_args+=("--filter" "Category!=Metered")
    fi
    test_args+=("--logger" "console;verbosity=detailed")
    
    print_info "Running: dotnet test ${test_args[*]}"
    dotnet test "${test_args[@]}"
    
    cd - > /dev/null
}

################################################################################
# Main Script
################################################################################

main() {
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --version)
                MCP_PACKAGE_VERSION="$2"
                shift 2
                ;;
            --filter)
                TEST_FILTER="$2"
                shift 2
                ;;
            --no-build)
                SKIP_BUILD=true
                shift
                ;;
            --license)
                LICENSE_PATH="$2"
                [ ! -f "$LICENSE_PATH" ] && print_error "License file not found: $LICENSE_PATH" && exit 1
                shift 2
                ;;
            --help)
                show_help
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
    
    # Header
    echo ""
    echo "╔════════════════════════════════════════════════════════════╗"
    echo "║  GroupDocs.Assembly.Mcp — Integration Test Runner (local) ║"
    echo "╚════════════════════════════════════════════════════════════╝"
    echo ""
    
    # Execution
    validate_environment
    
    if [ "$SKIP_BUILD" = false ]; then
        build_project
    else
        print_info "Skipping build step (--no-build specified)"
    fi
    
    # Run tests locally (more efficient than Docker for this use case)
    run_all_scenarios_locally
    
    print_header "Test Summary"
    print_success "All integration test scenarios completed!"
    echo ""
}

# Run main function
main "$@"
