---
name: skill-sync
description: MUST be read before starting any task, plan, or coding work, and again when finishing, for skills with `share: github`. Pull before starting. If a shared skill was edited, collect it and push it when finishing. This should trigger alongside workflow.
share: github
---

# Skill Sync

## Start

Run:

```bash
skillshare pull
```

## Finish

If the edited skill frontmatter metdata has `share: github`, for each modified skill, run:

```bash
skillshare collect <skill-name> -g
skillshare push -m "<message>"
```

`<message>` must follow the same rules as git commit messages.
