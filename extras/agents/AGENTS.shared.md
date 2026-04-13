## Shared Skill Sync Rules

Skills in `.ai/skills/` are shared across projects via Skillshare.

### Before starting any task:

Pull the latest shared skills:

```bash
.ai/skills/skillshare-sync/bin/ss pull
```

### After editing a skill file:

Share changes with all other projects and push to GitHub:

```bash
.ai/skills/skillshare-sync/bin/ss share <project-name>
```

Where `<project-name>` is the current project directory name (e.g., `diamond`, `home`, `namioto`).
