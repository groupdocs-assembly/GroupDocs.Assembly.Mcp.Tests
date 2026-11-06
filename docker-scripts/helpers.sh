#!/bin/bash
################################################################################
# Common helper functions for docker-scripts
# Source this file in other scripts: source "$(dirname "$0")/helpers.sh"
################################################################################

# Colors for consistent output formatting
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m'  # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}ℹ${NC}  $1"
}

log_success() {
    echo -e "${GREEN}✓${NC}  $1"
}

log_error() {
    echo -e "${RED}✗${NC}  $1"
}

log_warning() {
    echo -e "${YELLOW}⚠${NC}  $1"
}

log_debug() {
    if [ "${DEBUG:-0}" = "1" ]; then
        echo -e "${MAGENTA}🔧${NC} $1"
    fi
}

log_header() {
    local text="$1"
    local width=60
    local padding=$(( (width - ${#text}) / 2 ))
    local bar
    printf -v bar '%*s' "$width" ''
    bar="${bar// /=}"

    echo ""
    echo -e "${CYAN}${bar}${NC}"
    printf "%${padding}s${BLUE}%s${NC}\n" "" "$text"
    echo -e "${CYAN}${bar}${NC}"
    echo ""
}

log_section() {
    echo ""
    echo -e "${BLUE}→ $1${NC}"
}

# File and directory checks
check_file_exists() {
    local file=$1
    local description=${2:-"File"}
    
    if [ ! -f "$file" ]; then
        log_error "$description not found: $file"
        return 1
    fi
    log_success "$description found: $file"
    return 0
}

check_dir_exists() {
    local dir=$1
    local description=${2:-"Directory"}
    
    if [ ! -d "$dir" ]; then
        log_warning "$description not found: $dir (creating...)"
        mkdir -p "$dir" || {
            log_error "Failed to create $description: $dir"
            return 1
        }
        log_success "Created $description: $dir"
        return 0
    fi
    log_success "$description found: $dir"
    return 0
}

# Command availability checks
check_command() {
    local cmd=$1
    local description=${2:-"$cmd"}
    
    if ! command -v "$cmd" &> /dev/null; then
        log_error "$description is not installed or not in PATH"
        return 1
    fi
    
    # Get version if possible
    local version=""
    case "$cmd" in
        docker)
            version=$("$cmd" --version 2>/dev/null | cut -d' ' -f3 | tr -d ',')
            ;;
        dotnet)
            version=$("$cmd" --version 2>/dev/null)
            ;;
        git)
            version=$("$cmd" --version 2>/dev/null | awk '{print $3}')
            ;;
    esac
    
    if [ -n "$version" ]; then
        log_success "$description installed: $version"
    else
        log_success "$description is available"
    fi
    return 0
}

# Docker-specific functions
check_docker_daemon() {
    if ! docker ps &> /dev/null; then
        log_error "Docker daemon is not running or not accessible"
        return 1
    fi
    log_success "Docker daemon is accessible"
    return 0
}

docker_image_exists() {
    local image=$1
    if docker image inspect "$image" &> /dev/null; then
        return 0
    fi
    return 1
}

docker_pull_image() {
    local image=$1
    log_info "Pulling Docker image: $image"
    
    if docker pull "$image" 2>&1 | grep -q "Status: Downloaded\|Status: Image is up to date"; then
        log_success "Image pulled successfully"
        return 0
    else
        log_error "Failed to pull image: $image"
        return 1
    fi
}

# .NET specific functions
check_dotnet_sdk() {
    local required_version=${1:-10}
    
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK not found. Install .NET $required_version SDK"
        return 1
    fi
    
    local version=$(dotnet --version | cut -d. -f1)
    if [ "$version" -lt "$required_version" ]; then
        log_error ".NET SDK version $version found, but $required_version required"
        return 1
    fi
    
    log_success ".NET SDK version $(dotnet --version) is compatible"
    return 0
}

dotnet_restore() {
    local project_dir=$1
    log_section "Restoring .NET dependencies"

    if ( cd "$project_dir" && dotnet restore ); then
        log_success "Dependencies restored"
        return 0
    else
        log_error "Dependency restore failed"
        return 1
    fi
}

dotnet_build() {
    local project_dir=$1
    local config=${2:-Release}

    log_section "Building .NET project ($config configuration)"

    if ( cd "$project_dir" && dotnet build -c "$config" ); then
        log_success "Build completed successfully"
        return 0
    else
        log_error "Build failed"
        return 1
    fi
}

dotnet_test() {
    local project_dir=$1
    local config=${2:-Release}
    local filter=${3:-}
    shift 3 2>/dev/null || true

    log_section "Running tests"

    local -a args=("-c" "$config")
    [ -n "$filter" ] && args+=("--filter" "$filter")
    args+=("$@")

    ( cd "$project_dir" && dotnet test "${args[@]}" )
}

# Path handling for cross-platform compatibility
normalize_path() {
    local path=$1
    
    if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
        # Convert Windows paths to Unix-style for tools
        echo "$path" | sed 's|\\|/|g'
    else
        echo "$path"
    fi
}

# Sample documents management
list_sample_docs() {
    local sample_dir=$1
    
    if [ ! -d "$sample_dir" ]; then
        log_warning "Sample docs directory not found: $sample_dir"
        return 1
    fi
    
    local count=$(find "$sample_dir" -type f 2>/dev/null | wc -l)
    if [ "$count" -eq 0 ]; then
        log_info "No sample documents found"
        return 0
    fi
    
    echo ""
    log_section "Sample Documents ($count files):"
    find "$sample_dir" -type f -exec ls -lh {} \; | awk '{print "  " $9 " (" $5 ")"}'
    echo ""
}

validate_sample_docs() {
    local sample_dir=$1
    
    if [ ! -d "$sample_dir" ]; then
        log_warning "Files directory doesn't exist: $sample_dir"
        log_info "Creating empty directory for sample documents"
        mkdir -p "$sample_dir"
        return 0
    fi
    
    local count=$(find "$sample_dir" -type f 2>/dev/null | wc -l)
    if [ "$count" -eq 0 ]; then
        log_warning "Files directory is empty"
        log_info "Tests will use synthetic PDF/JPEG fixtures"
        return 0
    fi
    
    log_success "Found $count sample documents"
    return 0
}

# License file handling
check_license_file() {
    local license_path=$1
    
    if [ -z "$license_path" ]; then
        log_info "No license file specified (evaluation mode)"
        return 0
    fi
    
    if [ ! -f "$license_path" ]; then
        log_error "License file not found: $license_path"
        return 1
    fi
    
    log_success "License file found: $license_path"
    return 0
}

# Environment display
print_environment_info() {
    log_header "Environment Information"
    
    echo -e "OS Type:        ${CYAN}$OSTYPE${NC}"
    echo -e "Shell:          ${CYAN}$SHELL${NC}"
    echo -e "Bash Version:   ${CYAN}$BASH_VERSION${NC}"
    
    if command -v docker &> /dev/null; then
        echo -e "Docker:         ${CYAN}$(docker --version | cut -d' ' -f3 | tr -d ',')${NC}"
    fi
    
    if command -v dotnet &> /dev/null; then
        echo -e ".NET SDK:       ${CYAN}$(dotnet --version)${NC}"
    fi
    
    if command -v git &> /dev/null; then
        echo -e "Git:            ${CYAN}$(git --version | awk '{print $3}')${NC}"
    fi
    
    echo ""
}

# Cleanup functions
cleanup_docker_resources() {
    local compose_file=$1
    
    log_section "Cleaning up Docker resources"
    
    if [ ! -f "$compose_file" ]; then
        log_warning "Docker Compose file not found: $compose_file"
        return 0
    fi
    
    if docker compose -f "$compose_file" down -v 2>/dev/null; then
        log_success "Docker resources cleaned up"
        rm -f "$compose_file"
        return 0
    else
        log_error "Failed to clean up Docker resources"
        return 1
    fi
}

# Assert/validation functions
assert_command_exists() {
    if ! command -v "$1" &> /dev/null; then
        log_error "Required command not found: $1"
        exit 1
    fi
}

assert_file_exists() {
    if [ ! -f "$1" ]; then
        log_error "Required file not found: $1"
        exit 1
    fi
}

assert_dir_exists() {
    if [ ! -d "$1" ]; then
        log_error "Required directory not found: $1"
        exit 1
    fi
}

# Utility functions
prompt_user() {
    local message=$1
    local default=${2:-}
    
    if [ -n "$default" ]; then
        read -p "$(echo -e ${BLUE}$message [${default}]: ${NC})" -r
        REPLY="${REPLY:-$default}"
    else
        read -p "$(echo -e ${BLUE}$message: ${NC})" -r
    fi
    
    echo "$REPLY"
}

wait_for_port() {
    local port=$1
    local timeout=${2:-30}
    local elapsed=0
    
    log_info "Waiting for port $port to be available (timeout: ${timeout}s)..."
    
    while ! nc -z localhost "$port" 2>/dev/null && [ $elapsed -lt $timeout ]; do
        sleep 1
        elapsed=$((elapsed + 1))
    done
    
    if [ $elapsed -lt $timeout ]; then
        log_success "Port $port is now available"
        return 0
    else
        log_error "Timeout waiting for port $port"
        return 1
    fi
}

# Export all functions for use in other scripts
export -f log_info log_success log_error log_warning log_debug log_header log_section
export -f check_file_exists check_dir_exists check_command check_docker_daemon
export -f docker_image_exists docker_pull_image check_dotnet_sdk
export -f dotnet_restore dotnet_build dotnet_test normalize_path
export -f list_sample_docs validate_sample_docs check_license_file
export -f print_environment_info cleanup_docker_resources
export -f assert_command_exists assert_file_exists assert_dir_exists
export -f prompt_user wait_for_port
