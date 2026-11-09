#!/bin/bash
################################################################################
# GroupDocs.Assembly.Mcp — Docker-Compose Integration Test Runner
#
# Runs integration tests inside Docker containers with:
#   - Isolated test environment
#   - Mounted Files volume
#   - Cross-platform support (Windows/Linux/macOS)
#
# This script is useful for:
#   - CI/CD pipelines
#   - Testing in containerized environments
#   - Reproducing exact test conditions across machines
#
# Prerequisites:
#   - Docker or Docker Desktop installed and running
#   - Files folder with test fixtures (optional)
#
# Usage:
#   ./03_test-docker-compose.sh [OPTIONS]
#
# Options:
#   --version VERSION       Test specific package version (default: latest)
#                           Use "latest" or omit the flag to track nuget.org's
#                           most recent stable release. Pin (e.g. "26.11.0") for
#                           reproducible / shared / CI runs.
#   --filter PATTERN        Run only tests matching pattern
#   --license PATH          Path to GroupDocs license file
#   --keep                  Keep containers running (don't auto-cleanup)
#   --help                  Show this help message
#
# Examples:
#   ./03_test-docker-compose.sh                                  # latest stable
#   ./03_test-docker-compose.sh --version 26.11.0
#   ./03_test-docker-compose.sh --filter ReplaceText --keep
#
################################################################################

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_PROJECT_DIR="$PROJECT_ROOT/src/GroupDocs.Assembly.Mcp.Tests"
SAMPLE_DOCS_DIR="$PROJECT_ROOT/Files"
MCP_PACKAGE_VERSION="${MCP_PACKAGE_VERSION:-latest}"
TEST_FILTER=""
LICENSE_PATH=""
KEEP_CONTAINERS=false
BUILD_CONFIG="Release"

# Detect OS for path handling
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    # Git Bash on Windows - convert Windows paths
    SAMPLE_DOCS_DIR=$(cd "$SAMPLE_DOCS_DIR" 2>/dev/null && pwd || echo "$SAMPLE_DOCS_DIR")
    PROJECT_ROOT=$(cd "$PROJECT_ROOT" 2>/dev/null && pwd || echo "$PROJECT_ROOT")
fi

print_info() { echo -e "${BLUE}ℹ $1${NC}"; }
print_success() { echo -e "${GREEN}✓ $1${NC}"; }
print_error() { echo -e "${RED}✗ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠ $1${NC}"; }
print_header() { echo -e "\n${BLUE}════════════════════════════════════════════════════${NC}\n${BLUE}  $1${NC}\n${BLUE}════════════════════════════════════════════════════${NC}\n"; }

show_help() {
    grep '^#' "$0" | grep -E "^\s*#\s+(Usage|Options|Examples|Prerequisites)" | sed 's/^# //'
}

validate_environment() {
    print_header "Validating Environment"
    
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed"
        exit 1
    fi
    print_success "Docker: $(docker --version)"
    
    if ! docker ps &> /dev/null; then
        print_error "Docker daemon not running"
        exit 1
    fi
    print_success "Docker daemon is running"
    
    if [ ! -d "$TEST_PROJECT_DIR" ]; then
        print_error "Test project not found: $TEST_PROJECT_DIR"
        exit 1
    fi
    print_success "Test project: $TEST_PROJECT_DIR"
    
    if [ ! -d "$SAMPLE_DOCS_DIR" ]; then
        print_warning "Files not found, creating empty directory"
        mkdir -p "$SAMPLE_DOCS_DIR"
    else
        local count=$(find "$SAMPLE_DOCS_DIR" -type f 2>/dev/null | wc -l)
        print_success "Found $count sample documents"
    fi
}

create_docker_compose_file() {
    local compose_file="$PROJECT_ROOT/docker-compose.test.yml"

    # Build the filter fragment once, so it's a no-op when TEST_FILTER is empty.
    local filter_fragment=""
    [ -n "$TEST_FILTER" ] && filter_fragment="--filter FullyQualifiedName~$TEST_FILTER"

    # Optional license env + mount.
    local license_env=""
    local license_mount=""
    if [ -n "$LICENSE_PATH" ]; then
        license_env="      GROUPDOCS_LICENSE_PATH: /license/GroupDocs.Total.lic"
        license_mount="      - $LICENSE_PATH:/license/GroupDocs.Total.lic:ro"
    fi

    # Workspace is mounted RW because dotnet restore/test writes obj/ and bin/.
    # Files is mounted RO at /Files and the fixture reads
    # GROUPDOCS_MCP_SAMPLE_DOCS to copy real samples into its writable storage dir.
    # The test project spawns its own MCP server via `dnx`; no sidecar needed.
    cat > "$compose_file" << EOF
services:
  test-runner:
    image: mcr.microsoft.com/dotnet/sdk:10.0
    working_dir: /workspace/src/GroupDocs.Assembly.Mcp.Tests
    environment:
      DOTNET_NOLOGO: "true"
      DOTNET_CLI_TELEMETRY_OPTOUT: "1"
      GROUPDOCS_MCP_SAMPLE_DOCS: /Files
$license_env
    volumes:
      - $PROJECT_ROOT:/workspace
      - $SAMPLE_DOCS_DIR:/Files:ro
$license_mount
    command:
      - dotnet
      - test
      - -c
      - $BUILD_CONFIG
      - -p:McpPackageVersion=$MCP_PACKAGE_VERSION
      - --logger
      - "console;verbosity=normal"
      - --logger
      - "trx;LogFileName=test-results-docker.trx"
EOF

    # Append the filter argument only when set — YAML list items live below `command:`.
    if [ -n "$TEST_FILTER" ]; then
        cat >> "$compose_file" << EOF
      - --filter
      - FullyQualifiedName~$TEST_FILTER
EOF
    fi

    echo "$compose_file"
}

run_docker_tests() {
    local compose_file=$1

    print_header "Running Tests in Docker"

    print_info "Configuration:"
    echo "  Package Version: $MCP_PACKAGE_VERSION"
    [ -n "$TEST_FILTER" ] && echo "  Test Filter: $TEST_FILTER"
    [ -n "$LICENSE_PATH" ] && echo "  License: $LICENSE_PATH"
    echo ""

    cd "$PROJECT_ROOT"
    # Use a relative compose filename — on Git Bash, passing an absolute
    # /c/… path to docker.exe gets interpreted as a Windows relative path
    # (yielding C:\c\...). With cwd already PROJECT_ROOT, the basename is enough.
    local compose_rel
    compose_rel=$(basename "$compose_file")

    print_info "Pulling .NET SDK image (first run only)..."
    docker compose -f "$compose_rel" pull 2>/dev/null || true

    print_info "Running tests..."
    echo ""

    # --abort-on-container-exit returns the exit code of the first container to exit,
    # which is exactly what we want: propagate `dotnet test`'s exit code.
    if docker compose -f "$compose_rel" up --abort-on-container-exit --remove-orphans; then
        print_success "Tests passed!"
        return 0
    else
        print_error "Tests failed"
        return 1
    fi
}

cleanup() {
    local compose_file=$1
    
    if [ "$KEEP_CONTAINERS" = true ]; then
        print_warning "Containers kept running (use 'docker compose -f $compose_file down' to clean up)"
        return
    fi
    
    print_header "Cleaning Up"
    cd "$PROJECT_ROOT"
    local compose_rel
    compose_rel=$(basename "$compose_file")
    docker compose -f "$compose_rel" down -v 2>/dev/null || true
    rm -f "$compose_file"
    print_success "Cleanup completed"
}

main() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --version) MCP_PACKAGE_VERSION="$2"; shift 2 ;;
            --filter) TEST_FILTER="$2"; shift 2 ;;
            --license) 
                LICENSE_PATH="$2"
                [ ! -f "$LICENSE_PATH" ] && print_error "License not found: $LICENSE_PATH" && exit 1
                shift 2
                ;;
            --keep) KEEP_CONTAINERS=true; shift ;;
            --help) show_help; exit 0 ;;
            *) print_error "Unknown option: $1"; show_help; exit 1 ;;
        esac
    done
    
    echo ""
    echo "╔════════════════════════════════════════════════════╗"
    echo "║  GroupDocs.Assembly.Mcp — Docker Test Runner      ║"
    echo "╚════════════════════════════════════════════════════╝"
    echo ""
    
    validate_environment
    local compose_file=$(create_docker_compose_file)
    print_success "Docker Compose file: $compose_file"
    
    if run_docker_tests "$compose_file"; then
        cleanup "$compose_file"
        print_header "Success!"
        exit 0
    else
        print_error "Test execution failed"
        [ "$KEEP_CONTAINERS" = true ] && print_info "Containers still running for debugging"
        exit 1
    fi
}

main "$@"
