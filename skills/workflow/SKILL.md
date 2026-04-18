---
name: workflow
description: MUST be read before starting any task, plan, or coding work, and again when finishing. Defines branch rules, commit format, merge procedure, and cleanup steps. No work begins or ends without following this.

metadata:
  tags: [git, workflow, start, begin, task, plan, branch, finish, complete, end, merge, squash, cleanup]
  share: github
---

# Intent (Workflow)

Any task, plan, workstream or otherwise set of tasks a coding agent executes sequentially for user is called a workflow and must folow those instructions.

## Checklist

The whole workflow must be followed, you must complete all these steps.

Workflow Start:
 - [ ] Working tree cleaned
 - [ ] Skills synchronized
 - [ ] Git commit format understood
 - [ ] Feature branch and initial commit created

Workflow End:
 - [ ] Created marker tag
 - [ ] Merged squash
 - [ ] Verified Integrity
 - [ ] Feature branch deleted

## Rules

- Any work MUST folow these rules unless user override
- work MUST NOT start writing codeuntil all conditions in workflow start has been executed

# Workflow start

A workflow is called differently in different agents: plans, tasks, workstreams or others. They are all synonyms.

As soon as the user indicates the start of a new workflow, the woorkflow has started and you ust follow  the rues of the workflow start immediately.

- work never starts on a dirty tree. Ask the user for confirmation to stash dirty files, do a WIP git commit or turn it into a workflow that is now complete.
- all work MUST be done on a feature branch `f/<feature-id>`
- a feature branch MUST ONLY branch from the `main` branch, never from a feature brnch.
- Merge commits during work in a workflow are forbidden, unless overruled by the user.
- All work MUST be done on feature branches unless specifically allowed by the user with branch-name being (`f/<feature-id>`)

## Workflow Preparation


As soon as a user indicates wanting to start a workflow:
 - synchronize the skills
 - Use the description provided by the user or ask for the user for a short intent.
- Create a feature branch `f/<feature-id>` from the latest `main` branch. The `<feature-id>` should be a concise, human-readable identifier for the work being done (e.g., `f/add-login-feature`, `f/fix-payment-bug`, `f/refactor-auth-module`).
 - Create a commit describing the intent of the future branch, and a trailer called `Base` with the SHA of the commit from which the branch was created. with a `tada` gitmoji. This commit serves as the anchor for the branch and the starting point for all future work in this workflow.

# Workflow end

Ending the workflow must follow the following steps in this order.

## Clean tree

Agents leave files behind tracking their work.

If files are included by .gitignore as preserve `!<path>` or ignore `<path>`, they are to be commited, otherwise alert the user and propose adding the directory or the file  to `.gitignore`, with a recommendation.
Examples:
- OpenCode plan files (`.opencode/plans/`, `plan.md`, or similar)
- Agent task files, scratchpads, or temporary reasoning files created by the tool
- Any file the tool created to track its own progress, not for the project
- Propose to the user to add any such file to the `.gitignore` file
- Do not delete files without user confirmation

Task files always get commited:
- `HANDOVER.md` — cross-session context for the next agent
- `TODO.md` — project-level work tracking yet to do. Move completed tasks to `DONE.md`
- `DONE.md` — project-level work done.
- `AGENTS.shared.md` — shared rules for all agents on this project
- Memory files under `.claude/projects/`

## ⛔ User approval gate

STOP. Before doing anything else, you MUST ask the user for explicit approval to merge and push.
Present a summary of the commits (branch name, commit count, HEAD sha) and wait for confirmation.
Do NOT proceed with any of the steps below until user confirmation.

## Marker tag

You must add a tag named `archive/<feature-id>` to the feature branch you're merging.

## Merge squash

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
- `<lead-time>` Human-readable lead time (e.g., "2d 8h", "1m 2d"), the time it took between the initial feature branch commit and the merge commit.
- For anything else follow the rules in the git-commit skill.

After creating the squash commit, write the commit count as a git note on the new HEAD:

```
git notes add -m "commit-count: $N"
```

When pushing, always include notes so CI can read the count for versioning:

```
git push origin main refs/notes/commits
```

## Verify squash integrity

After the squash commit, verify that no content was lost and the metadata is correct:

```bash
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

## Delete branch

Delete the feature branch. Use `git branch -D` (force) because squash merges do not preserve ancestry — git's `-d` safety check will always report "not fully merged" even when the content is identical. The archive tag and the integrity check above are the safety net.
