---
name: skill-sync
description: "MUST be read before starting any task, plan, or coding work, and again when finishing, for skills with `share: github`. Pull before starting. If a shared skill was edited, collect it and push it when finishing. This should trigger alongside orkflow."
metadata:
  share: github
---

# Skill Sync
Skills are shared across projects and must stay consistent.

## After the first commit on a feature branch

Update to the latest skills. 

### Update skills

```bash
skillshare update --all --project --force
skillshare sync --all --force -p
```

### Commit updated files

If the above commands change the repository, all changes must be committed immediately with the message `Update skills` and the package gitmoji.

## When work is complete

If any skill with `share: github` in its frontmatter is modified, it must be written back to its share repository.

Symlink files pointing to the .skillshare directory, and the diretory itself must always be committed in a separate commit, as above.

### Git Push

```bash
git -C .skillshare/skills/_origin add -A
git -C .skillshare/skills/_origin commit -m `<message>`
git -C .skillshare/skills/_origin push || echo "No changes to commit"
```
`<message>` must follow standard git commit rules.
