# Shared agent instructions

This file contains shared instructions for all agents and all projects.

It MUST NOT be:
- modified
- deleted
- moved
- renamed

unless explicitly requested by the user.

---

## Core principles

- Any rule can be overruled by the user
- The user's architecture, requirements, and designs are mandatory
- The agent MAY propose changes but MUST NOT apply them without user approval

---

## Mandatory files

### README.md

Describes the repository for humans.

Rules:
- Uses sections starting with `# <header>`
- Only update sections impacted by your changes. In the case of a mono repo, only update the part of the file related to your changes' area.

Must include:
- WHAT the repository does (intent, not implementation), max 5 lines
- Installation instructions
- Usage example (end-to-end, concise; may include comments)

---

### ARCHITECTURE.md

- High-level system structure
- How components fit together
- WHY key decisions were made

---

### ROADMAP.md

- Future plans only

Each entry MUST include:
- WHAT
- WHY
- Date added

Informational only (not an execution plan)

---

### AGENTS.shared.md

- Shared instructions across all projects
- MUST NOT be modified unless explicitly requested

---

## Repository structure

- Group files by technology
- Source code MUST be in `src/`
- Tests MUST be in `tests/` when applicable

Example:

/
  python/
    src/
    tests/
  opentofu/
    src/

---

## Software principles

- SOLID
- KISS
- Composition over inheritance
- Do NOT write speculative code
- Implement only what is required for the current feature
- Prefer self-descriptive code over comments
- Avoid redundant naming (e.g. DTO, Service, Component, State)

---

## Editing rules

- Keep changes local unless broader changes are required
- Do NOT change architecture without user approval
- Reuse existing patterns before introducing new ones

---

## Stop condition

Stop when:
- the requested change is complete
- the result is sufficiently verified for the task