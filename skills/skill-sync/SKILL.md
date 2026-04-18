---
name: skill-sync
description: "MUST be read before starting any task, plan, or coding work, and again when finishing, for skills with `share: github`. Pull before starting. If a shared skill was edited, collect it and push it when finishing. This should trigger alongside orkflow."
metadata:
  share: github
---

# Skill Sync
Skills are shared across projects and must stay consistent.

## Before starting any work


### Update skills

```bash
skillshare update --all --project --force
skillshare sync --all --force -p
```

## When work is complete

If any skill with `share: github` in its frontmatter was modified, it must be written back to its repository.

### Git Push

```bash
git -C .skillshare/skills/_origin add -A
git -C .skillshare/skills/_origin commit -m `<message>`
git -C .skillshare/skills/_origin push || echo "No changes to commit"
```
`<message>` must follow standard git commit rules.

### Refresh

Re-apply the Update Skills section.

## Rules

- DO NOT use other `skillshare`
- Always run `git status` before committing
- If there are no changes, continue without failing
