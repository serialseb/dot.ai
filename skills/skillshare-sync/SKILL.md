---
name: skillshare-sync
description: Use when starting work, finishing work, or after editing a skill file. Keeps skills synchronized across all projects via Skillshare.
metadata:
  tags: [skillshare, sync, workflow, skills]
---

# Skill Synchronization

Skills are shared across projects using Skillshare. The `bin/ss` script in this skill directory wraps the sync workflow.

## When starting work

Pull the latest shared skills before beginning:

```bash
.ai/skills/skillshare-sync/bin/ss pull
```

## When finishing work

If you edited any skill file during the session, share changes with all other projects and push to GitHub:

```bash
.ai/skills/skillshare-sync/bin/ss share <project-name>
```

Where `<project-name>` is the name of the current project directory (e.g., `diamond`, `home`, `namioto`).

## Commands

| Command | What it does |
|---|---|
| `bin/ss pull` | Pull latest skills from GitHub, sync to all projects |
| `bin/ss share <project> [message]` | Collect changes, sync everywhere, push to GitHub |
| `bin/ss sync` | Redistribute source skills to all projects (no GitHub) |
| `bin/ss status` | Show current sync state |
