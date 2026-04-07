---
name: workflow
description: Use this when starting or stoppping work on code, You MUST NOT begin coding without executing this tool.
metadata:
  tags: [git, push, merge, plan, task, workflow, finish, complete, end]
---

# Rules

- work is always within a workflow
- work MUST NOT start until all conditions in workflow strat section has been exectted

# Workflow

Any task, plan, workstream or otherwise set of tasks a coding agent executes sequentially for user is called a workflow.

# Checklist

Start:
 - [ ] Feature branch created
 - [ ] Working tree clean
 - [ ] Git commit format understood

End:
 - [ ] Merge squash
 - [ ] Marker tag
 - [ ] Feature branch deleted

## Workflow start

Before any work is started, by the user or the agent, a workflow has started.
- work never starts on a dirty tree. Ask the user for confirmation to sash dirty files or do a WIP git commit
- all work MUST be done on a feature branch `f/<feature-id>`
- a feature branch MUST ONLY branch from the `main` branch, never from a feature brnch.

A workflow is called differently in different agents: plans, tasks, workstreams or others. They reore

- Merge commits are forbidden.
- All work MUST be done on feature branches unless specifically allowed by the user with branch-name being (`f/<feature-id>`)

## Workflow end

Ending the workflow must follow the following steps in this order.

### ⛔ User approval gate

STOP. Before doing anything else, you MUST ask the user for explicit approval to merge and push.
Present a summary of what will be merged (branch name, commit count, HEAD sha) and wait for confirmation.
Do NOT proceed with any of the steps below until the user says yes.

### Marker tag

You must add a tag named `archive/<feature-id>` to the feature branch you're merging.

### Merge squash

Before merging, record the commit count of the feature branch:

```
N=$(git rev-list main..f/<feature-id> --count)
```

You must merge squash the feature branch onto the `main` branch, folowing this template.

```
<gitmoji> <subject>

🎯 <branch-head> 📦 <commits> ⌛ <lead-time>

<body>

Branch: <branch>
Head: <brach-head>
Co-authored-by: <model> <junie@serialseb.com>
```

- `<sha>` - Short SHA of feature branch HEAD (the 🏁 commit)
- `<commits>` - Number of commits squashed
- `<lead-time>` - Human-readable lead time (e.g., "2d 8h", "1m 2d") from the first commit of the branch (not fron the commit from which you branched, normally main)
- `<branch>` - The feature branch you are merging
- `<lead-time>` Human-readable lead time (e.g., "2d 8h", "1m 2d"), the time it took between the first commit and the merge commit.
- For anything else follow the rules in the git-commit skill.

After creating the squash commit, write the commit count as a git note on the new HEAD:

```
git notes add -m "commit-count: $N"
```

When pushing, always include notes so CI can read the count for versioning:

```
git push origin main refs/notes/commits
```

### Verify squash integrity

After the squash commit, verify that no content was lost and the metadata is correct:

```
# Tree content must match
SQUASH_TREE=$(git rev-parse HEAD^{tree})
BRANCH_TREE=$(git rev-parse archive/<feature-id>^{tree})
[ "$SQUASH_TREE" = "$BRANCH_TREE" ] || echo "ERROR: tree mismatch"

# Commit count must match rev-list
NOTED_COUNT=$(git notes show HEAD 2>/dev/null | grep commit-count | cut -d' ' -f2)
ACTUAL_COUNT=$(git rev-list $(git merge-base main~ archive/<feature-id>)..archive/<feature-id> --count)
[ "$NOTED_COUNT" = "$ACTUAL_COUNT" ] || echo "ERROR: commit count mismatch (noted=$NOTED_COUNT actual=$ACTUAL_COUNT)"
```

If either check fails, do NOT delete the branch. Investigate and fix the squash commit first.

### Delete branch

Delete the feature branch. Use `git branch -D` (force) because squash merges do not preserve ancestry — git's `-d` safety check will always report "not fully merged" even when the content is identical. The archive tag and the integrity check above are the safety net.

### Complete agent task

If you keep files to track tasks, workflows, plans or otherwise, and they are no longer needed, delete them.