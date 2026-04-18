---
name: coding-tofu
description: Use for any read or edit of Terraform / OpenTofu code. Enforce clean, minimal, readable OpenTofu module structure
metadata:
  tags: [opentofu, terraform, infrastructure, module]
  share: github
---
# OpenTofu — Clean Module Guidelines

## Definitions
- Root module (project): the entrypoint configuration you run (`tofu apply`)
- Child module: a reusable module called by the root module

---

## Root Module (Project)

### Purpose
- Orchestrates infrastructure
- Wires modules together
- Contains environment-specific configuration

### Structure
- Prefer clarity over reuse
- Keep logic easy to scan
- May be opinionated and less reusable
- Can directly define resources if it improves readability

### Files
- main.tf — module calls and/or resources
- variables.tf — inputs (optional)
- outputs.tf — outputs (optional)

---

## Child Modules

### Purpose
- Encapsulate reusable logic
- Reduce duplication across projects

### Rules
- Must have a single, clear responsibility
- Do not create unless reuse or complexity justifies it
- Do not wrap a single resource unless reuse is proven

### Structure
- Keep modules small but meaningful
- Avoid deep nesting (modules calling many layers of modules)
- Prefer flat composition

### Interface
- All inputs via variables (no hidden dependencies)
- Expose only necessary outputs
- Do not hardcode environment-specific values

### Files
- main.tf — resources
- variables.tf — required inputs with descriptions
- outputs.tf — outputs with descriptions

---

## Dependencies
- Pass values explicitly between modules
- Do not rely on implicit or hidden coupling
- Use outputs only when necessary

---

## Documentation
- Describe only what is not obvious
- Keep README short and practical
- Variables and outputs must have concise descriptions

---

## Design Principles
- Readability first, reuse second
- Prefer duplication over premature abstraction
- Root module: optimized for clarity
- Child modules: optimized for reuse

---

## Avoid
- Over-modularization
- Deep module nesting
- Single-resource modules without clear reuse
- Excessive outputs
- Overuse of locals
- Clever abstractions that reduce readability

---

## Rule of Thumb
If a module cannot be understood in ~30 seconds, it is too complex.