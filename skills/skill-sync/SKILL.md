---
name: skill-sync
description: MUST be read before starting any task, plan, or coding work, and again when finishing, for skills with `share: github`. Pull before starting. If a shared skill was edited, collect it and push it when finishing. This should trigger alongside workflow.
share: github
---

# Skill Sync
Sills are shared across projects and must stsy consistent.

## Before strting work

Run:

```bash
skillshare pull
```

## When work is complete

If any skill with `share: github` in its frontmatter was modified:

```bash
cd .skillshare/skills/_origin
git status
git add .
git commit -m "<message>" || echo "No changes to commit"
git push origin main
```

`<message>` must follow standard git commit rules.

## Rules

- DO NOT use other `skillshare`
- Always run `git status` before committing
- If there are no changes, continue without failing