#!/bin/bash
################################################################################
# 01 — Setup Verification
#
# Validates that your environment is ready for running integration tests.
# Run this before executing 02_test-all-scenarios.sh for the first time.
#
# Usage:
#   ./01_verify-setup.sh [--verbose]
#
################################################################################

set -e

VERBOSE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --verbose) VERBOSE=true; shift ;;
        --help) echo "Usage: ./01_verify-setup.sh [--verbose]"; exit 0 ;;
        *) shift ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

PASSED=0
FAILED=0
WARNINGS=0

print_header() {
    echo ""
    echo -e "${BLUE}════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}════════════════════════════════════════════════════${NC}"
    echo ""
}

print_pass() {
    echo -e "${GREEN}✓${NC} $1"
    PASSED=$((PASSED + 1))
}

print_fail() {
    echo -e "${RED}✗${NC} $1"
    FAILED=$((FAILED + 1))
}

print_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
    WARNINGS=$((WARNINGS + 1))
}

print_info() {
    # Guard explicitly with if/else so `set -e` doesn't abort when VERBOSE=false
    # (a single `[ … ] && …` would return non-zero and kill the script).
    if [ "$VERBOSE" = true ]; then
        echo -e "${BLUE}ℹ${NC} $1"
    fi
}

check_command() {
    local cmd=$1
    local name=$2
    
    if command -v "$cmd" &> /dev/null; then
        local version=$("$cmd" --version 2>/dev/null | head -n1 || echo "")
        print_pass "$name: ${version:-(installed)}"
        return 0
    else
        print_fail "$name: NOT FOUND"
        return 1
    fi
}

main() {
    echo ""
    echo "╔════════════════════════════════════════════════════╗"
    echo "║  GroupDocs.Assembly.Mcp — Setup Verification      ║"
    echo "╚════════════════════════════════════════════════════╝"
    echo ""
    
    # Get script directory and project root
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
    
    print_info "Script Directory: $SCRIPT_DIR"
    print_info "Project Root: $PROJECT_ROOT"
    echo ""
    
    # ========== Required Tools ==========
    print_header "REQUIRED TOOLS"
    
    check_command "bash" "Bash Shell" || print_fail "Bash 4.0 or higher required"
    check_command "docker" "Docker" || print_fail "Docker Desktop or Docker Engine required"
    check_command "git" "Git" || print_fail "Git required (optional, but recommended)"
    check_command "dotnet" ".NET SDK" || print_warn ".NET 10 SDK required (unless using --no-build)"
    
    echo ""
    
    # ========== Docker Daemon ==========
    print_header "DOCKER DAEMON"
    
    if docker ps &> /dev/null; then
        local docker_info=$(docker version --format '{{.Server.Version}}')
        print_pass "Docker daemon is running (version: $docker_info)"
    else
        print_fail "Docker daemon is NOT running"
        print_info "Start Docker Desktop or run: sudo systemctl start docker"
    fi
    
    echo ""
    
    # ========== Project Structure ==========
    print_header "PROJECT STRUCTURE"
    
    # Check test project
    if [ -d "$PROJECT_ROOT/src/GroupDocs.Assembly.Mcp.Tests" ]; then
        print_pass "Test project found"
    else
        print_fail "Test project not found: $PROJECT_ROOT/src/GroupDocs.Assembly.Mcp.Tests"
    fi
    
    # Check csproj
    if [ -f "$PROJECT_ROOT/src/GroupDocs.Assembly.Mcp.Tests/GroupDocs.Assembly.Mcp.Tests.csproj" ]; then
        print_pass "Test project file found"
    else
        print_fail "Test csproj not found"
    fi
    
    # Check Files
    if [ -d "$PROJECT_ROOT/Files" ]; then
        local doc_count=$(find "$PROJECT_ROOT/Files" -type f 2>/dev/null | wc -l)
        if [ "$doc_count" -gt 0 ]; then
            print_pass "Files found ($doc_count files)"
        else
            print_warn "Files exists but is empty (tests will use synthetic fixtures)"
        fi
    else
        print_warn "Files not found (will be created automatically)"
    fi
    
    # Check scripts
    for script in 00_quick-start.sh 02_test-all-scenarios.sh 03_test-docker-compose.sh 04_run-server-with-samples.sh helpers.sh; do
        if [ -f "$SCRIPT_DIR/$script" ]; then
            print_pass "Script found: $script"
        else
            print_fail "Script not found: $script"
        fi
    done
    
    echo ""
    
    # ========== Docker Images ==========
    print_header "DOCKER IMAGES"
    
    local mcp_image="ghcr.io/groupdocs-assembly/assembly-net-mcp:latest"
    local sdk_image="mcr.microsoft.com/dotnet/sdk:10.0-alpine"
    
    print_info "Checking availability of required Docker images..."
    echo ""
    
    if docker image inspect "$mcp_image" &> /dev/null; then
        print_pass "MCP server image available locally"
    else
        print_warn "MCP server image not cached locally (will pull on first run)"
        print_info "  To pre-pull: docker pull $mcp_image"
    fi
    
    if docker image inspect "$sdk_image" &> /dev/null; then
        print_pass ".NET SDK image available locally"
    else
        print_warn ".NET SDK image not cached locally (will pull on first run)"
        print_info "  To pre-pull: docker pull $sdk_image"
    fi
    
    echo ""
    
    # ========== Network Connectivity ==========
    print_header "NETWORK CONNECTIVITY"

    # Use curl on all platforms — `ping -c 1` is Linux/macOS; Windows ping uses -n.
    if curl -s --max-time 5 -o /dev/null https://www.google.com; then
        print_pass "Internet connectivity OK"
    else
        print_warn "Internet connectivity check failed (may affect image pulls)"
    fi

    if curl -s --max-time 5 -o /dev/null https://api.nuget.org/v3/index.json; then
        print_pass "NuGet.org is reachable"
    else
        print_warn "NuGet.org not reachable (may affect package restoration)"
    fi
    
    echo ""
    
    # ========== Optional Tools ==========
    print_header "OPTIONAL TOOLS"

    # These are nice-to-have; don't count their absence as a failure.
    if command -v nc &> /dev/null; then
        print_pass "netcat: $(nc -h 2>&1 | head -n1)"
    else
        print_warn "netcat not found (used for port checking)"
    fi

    if command -v jq &> /dev/null; then
        print_pass "jq: $(jq --version 2>/dev/null)"
    else
        print_warn "jq not found (useful for JSON test results)"
    fi
    
    echo ""
    
    # ========== Summary ==========
    print_header "SUMMARY"
    
    echo -e "Passed:   ${GREEN}$PASSED${NC}"
    echo -e "Failed:   ${RED}$FAILED${NC}"
    echo -e "Warnings: ${YELLOW}$WARNINGS${NC}"
    echo ""
    
    if [ $FAILED -eq 0 ]; then
        echo -e "${GREEN}✓ Setup verification PASSED!${NC}"
        echo ""
        echo "You can now run:"
        echo -e "  ${BLUE}cd $SCRIPT_DIR${NC}"
        echo -e "  ${BLUE}./02_test-all-scenarios.sh${NC}"
        echo ""
        return 0
    else
        echo -e "${RED}✗ Setup verification FAILED!${NC}"
        echo ""
        echo "Please fix the errors above before running tests."
        echo "For help, see: $SCRIPT_DIR/README.md"
        echo ""
        return 1
    fi
}

main
