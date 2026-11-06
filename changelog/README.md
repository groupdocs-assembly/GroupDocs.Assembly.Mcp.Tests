# Changelog

Per-change notes for the GroupDocs.Assembly.Mcp integration tests repo, captured
**per commit or logical change set**. Each entry is a separate file named
`NNN-short-slug.md` where `NNN` is a zero-padded sequential number.

## Naming convention

```
001-initial-commit.md
002-add-docker-matrix.md
003-fix-windows-dnx-resolution.md
```

- `NNN` — increments strictly (never reused, even if a change is reverted)
- `short-slug` — kebab-case, imperative, ≤ 6 words

## Per-entry structure

```markdown
---
id: 001
date: 2026-04-23
package-under-test: 26.5.0   # version of GroupDocs.Assembly.Mcp the suite targets
type: feature | fix | refactor | docs | chore | breaking
---

# Short human title

## What changed
- Bullet list of visible changes (new tests, new guides, workflow updates).

## Why
Short rationale. Skip if obvious from the title.

## Migration / impact
Only when consumers of this repo need to do something (new env var, renamed
fixture, workflow hook change). Omit otherwise.
```

## Version coupling

Entries track the **integration-tests repo**, not the server. The
`package-under-test` field records which `GroupDocs.Assembly.Mcp` version the
tests were written against — useful when the server's tool surface evolves.
