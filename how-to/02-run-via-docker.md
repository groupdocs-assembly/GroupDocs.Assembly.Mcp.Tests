# Run via Docker

The MCP server is published as a container image to two registries:

- `ghcr.io/groupdocs-assembly/assembly-net-mcp` — GitHub Container Registry (primary)
- `docker.io/groupdocs/assembly-net-mcp` — Docker Hub (mirror)

Each release is tagged with its version (`:26.11.0`) and `:latest`.

> **No native setup needed.** The image already bundles `libgdiplus` (for presentation
> templates) and the fonts PDF/XPS output needs. Nothing to `apt-get` or `brew install`.
>
> **Linux hosts: run as your user.** The image runs as a non-root user (uid 1654), and a
> bind mount keeps its host owner, so every write into `/data` fails with
> `Permission denied` unless you add `--user "$(id -u):$(id -g)"` to `docker run`. JSON
> client configs run no shell, so put the numbers in: `"--user", "1000:1000"`. Docker
> Desktop on Windows and macOS does not need this.

## Prerequisites

```bash
docker --version
# Docker 20.10+ is fine; any recent version works
docker info        # confirms the daemon is running
```

On **Windows / macOS**, the daemon ships inside [Docker Desktop](https://www.docker.com/products/docker-desktop) — make sure it's installed and started before running any of the commands below. Symptom of a stopped daemon: `failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine` (Windows) or `Cannot connect to the Docker daemon at unix:///var/run/docker.sock` (macOS / Linux without Desktop).

On **Windows specifically**, Docker Desktop must have access to the drive your storage folder lives on. With the WSL2 backend (default since 4.x) this is automatic; with the legacy Hyper-V backend, enable it under **Settings → Resources → File sharing**.

## One-off run

```bash
docker run --rm -i \
  -v $(pwd)/documents:/data \
  ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0
```

- `--rm` — delete the container when it exits.
- `-i` — **required** — keeps stdin open so the MCP client can send JSON-RPC. Do NOT add `-t` (that would allocate a TTY and break the JSON stream).
- `-v $(pwd)/documents:/data` — mount the folder containing your files at `/data` inside the container.

The image sets `ENV GROUPDOCS_MCP_STORAGE_PATH=/data` and declares `VOLUME /data`, so the client tools see filenames relative to whatever you mount.

## Pinned vs always-latest

Each release pushes both `:<version>` (e.g. `:26.11.0`) and updates `:latest` to point at it. Pick the tag that matches how you want to handle upgrades:

```bash
docker pull ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0   # pinned to exact version
docker pull ghcr.io/groupdocs-assembly/assembly-net-mcp:latest   # floats to most recent push
```

### The Docker cache gotcha

Unlike `dnx`, Docker is **sticky**. Once you've pulled `:latest` at any point, every subsequent `docker run :latest` reuses the cached image — Docker does **not** check the registry again until you explicitly refresh:

```bash
# Refresh manually (pull on demand — recommended for periodic upgrade cadences):
docker pull ghcr.io/groupdocs-assembly/assembly-net-mcp:latest

# OR force a registry probe on every container start (auto-refresh, slower startup):
docker run --pull always --rm -i \
  -v "$(pwd)/documents:/data" \
  ghcr.io/groupdocs-assembly/assembly-net-mcp:latest
```

| Tag strategy | Behaviour | Best for |
|---|---|---|
| `:26.11.0` | Locked to that release. No surprise upgrades. | Committed configs, CI, shared team setups. |
| `:latest` (default `--pull missing`) | Stays on the version you first pulled. Manual `docker pull :latest` to refresh. | Solo devs upgrading on a schedule (e.g. once a month). |
| `:latest` + `--pull always` | Probes registry on every container start. | Always-current dev machines; tolerate +1–10s startup. |

> Docker tags don't support npm-style ranges (`^26.4`, `~26.4`) — it's pin-exact, `:latest`, or any custom tag the publisher pushes. The MCP image only publishes version tags + `:latest`.

### Verifying which version `:latest` resolved to

```bash
docker inspect ghcr.io/groupdocs-assembly/assembly-net-mcp:latest \
  --format '{{index .Config.Labels "org.opencontainers.image.version"}}'
```

Or once the server is running, the MCP `initialize` response includes `serverInfo.version` — same authoritative source as the NuGet flow ([01 — NuGet § Verifying version at runtime](01-install-from-nuget.md#verifying-version-at-runtime)).

## Smoke test

```bash
(
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}'
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
  sleep 2
) | docker run --rm -i \
    -v $(pwd)/documents:/data \
    ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0 2>/dev/null
```

Expected: two JSON-RPC responses on stdout. The second lists `assemble_document`,
`assemble_per_record`, `get_template_fields` and the other tools with their descriptions and input schemas.

## docker-compose

A reference compose file lives at [examples/docker-compose.yml](../examples/docker-compose.yml):

```yaml
services:
  groupdocs-assembly-mcp:
    image: ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0
    stdin_open: true
    tty: false
    environment:
      GROUPDOCS_MCP_STORAGE_PATH: /data
    volumes:
      - ./documents:/data
```

Run with:

```bash
docker compose up
```

Compose is useful for local development, but MCP clients like Claude Desktop / VS Code expect a process they can launch themselves over stdio — they don't typically connect to a compose service. For those clients, point the `command` at `docker run` directly.

## Using the image from MCP clients

### Claude Desktop

```json
{
  "mcpServers": {
    "groupdocs-assembly": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/absolute/path/to/documents:/data",
        "ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0"
      ]
    }
  }
}
```

### VS Code / GitHub Copilot

See [examples/vscode-mcp.json](../examples/vscode-mcp.json) for the `dnx` variant. For Docker, swap `command` to `docker` and `args` to the `run` invocation above.

## Providing a license

Mount the `.lic` file read-only and point the env var at the mount path:

```bash
docker run --rm -i \
  -v $(pwd)/documents:/data \
  -v $(pwd)/secrets/GroupDocs.Total.lic:/licenses/GroupDocs.Total.lic:ro \
  -e GROUPDOCS_LICENSE_PATH=/licenses/GroupDocs.Total.lic \
  ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0
```

Without the license, every tool still works, but generated documents carry evaluation marks
(see [01 — NuGet](01-install-from-nuget.md#license)).

## Verifying the image

```bash
# Inspect the image (entrypoint, env, user)
docker inspect ghcr.io/groupdocs-assembly/assembly-net-mcp:26.11.0 \
  --format '{{json .Config}}' | jq

# Expected:
# - Entrypoint: ["dotnet", "GroupDocs.Assembly.Mcp.dll"]
# - Env contains: GROUPDOCS_MCP_STORAGE_PATH=/data
# - User: 1654 (the .NET image's non-root "app" user)
```

The image runs as a non-root user (uid 1654). On a Linux host a bind mount keeps its
host owner, so the server can read your templates but cannot write generated
documents into the folder — pass `--user "$(id -u):$(id -g)"` to `docker run`, as
shown at the top of this guide.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `failed to connect to the docker API at npipe://...` (Windows) or `Cannot connect to the Docker daemon at unix:///var/run/docker.sock` | Docker daemon not running | Start Docker Desktop (Windows / macOS) or `sudo systemctl start docker` (Linux). |
| `error during connect: ... mounts denied` / mount silently empty (Windows) | Drive not shared with Docker Desktop | Settings → Resources → File sharing → add the drive. With WSL2 backend this is usually automatic; with Hyper-V backend it's manual. |
| Client says "server crashed" immediately | Passed `-t` along with `-i` | Remove `-t`. MCP needs a clean stdio pipe. |
| Tools see no files | Mount path / env var mismatch | Confirm you mounted to `/data` and didn't override `GROUPDOCS_MCP_STORAGE_PATH`. |
| Permission denied writing generated documents | Host mount is read-only or uid mismatch | Make the mount writable. `-v ./documents:/data` (not `:ro`). |
| `manifest unknown` / can't pull image | Version tag doesn't exist on that registry | Check [ghcr.io/groupdocs-assembly/assembly-net-mcp](https://github.com/groupdocs-assembly/GroupDocs.Assembly.Mcp/pkgs/container/assembly-net-mcp) for available tags. |
| `:latest` ran but didn't pick up a new release | Docker reuses cached image — `docker run :latest` does not auto-pull | `docker pull ghcr.io/groupdocs-assembly/assembly-net-mcp:latest` before running, or add `--pull always` to your `docker run`. |

## Next steps

- [05 — Use with VS Code / Copilot](05-use-with-vscode-copilot.md) — Docker launcher config
- [03 — MCP registry](03-verify-mcp-registry.md) — confirm the container is listed correctly
- [06 — Integration tests](06-run-integration-tests.md) — exercise the image end-to-end
