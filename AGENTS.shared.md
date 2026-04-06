## Submodule Sync Rules

### Before starting any task:

Run:
git submodule update --remote --merge

### After finishing a task (only if there are changes):

Step 1 — push changes inside the submodule:
cd .ai
git diff --quiet && git diff --cached --quiet || (git add -A && git commit -m "sync" && git push)

Step 2 — update the submodule pointer in the parent repo:
cd ..
git diff --quiet && git diff --cached --quiet || (git add .ai && git commit -m "update submodule ref" && git push)

Never commit or push if there are no changes.
