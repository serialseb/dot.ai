---
name: skill-sync
description: MUST be read before starting any task, plan, or coding work, and again when finishing, for skills with `share: github`. Pull before starting. If a shared skill was edited, collect it and push it when finishing. This should trigger alongside workflow.
metadata:
  share: github
---

# Skill Sync
Sills are shared across projects and must stsy consistent.

## Before strting work


### Update skills

```bash
skillshare update --all --project
skillshare init --project --discover
skillshare sync --all --project
```

## When work is complete

If any skill with `share: github` in its frontmatter was modified, it must be written back to its repository.

### Git Push

```bash
cd .skillshare/skills/_origin
git status
git add .
git commit -m "<message>" || echo "No changes to commit"
git push origin main
```
`<message>` must follow standard git commit rules.

### Refresh

Re-apply the Update Skills section.

## Rules

- DO NOT use other `skillshare`
- Always run `git status` before committing
- If there are no changes, continue without failing
