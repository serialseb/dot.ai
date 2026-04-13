---
name: git-commit
description: Use when committing files to git. It describes format and content to include in the commit messages that MUST be followed.
share: github
compatibility: Requires git
metadata:
  tags: [git, commit, gitmoji, plan, task]
---

# git commits (MUST)

## Checklist
- [ ] Tests ran if exist
- [ ] Fprmat applied
- [ ] Maximum line length applied
- [ ] No commits on main unless user override
- [ ] Use git commit only unless user override
- [ ] Trailers correct

## Checklist

- Commit for each successful set of changes. Only commit the files you touched.
  - If you see multiple sets of changes in the working tree, split into multiple commits.
  - When the user has made changes to .md files, commit them separately.
- All rules can be overriden by the user.

## Forbidden

Unless otherwise requested, you can only write in non-destructive manners (git stash,git commits, git workspace, but not git push, git rebase, git cherry-pick). If you need those, ASK the user.

## Style

- The `<subject>` must describe the change in imperative form, e.g. "Encapsulate class X". Avoid starting words with too generic meaning like "add".
- The `<subject>` always starts with a capital letter and never ends with punctuation  
- Both the subjet and the body MUST explain the WHY not the HOW. Avoid technical details and if absolutely ncesessary keep then short. Do not prefix it with `HOW` or other prefixes, body must contain only explanationl

## Format

This format must be followed.

``` 
<gitmoji> <subject>

<test-emoji> x/y

<body>

Branch: <branch-name>
Co-authored-by: <model> <junie@serialseb.com>
```

- the subject line must. be <= 52 garacters
- In the Co-authored-by, `<model>` is the LLM model name and version, the email address enclosed in brackets MUST be left as is. 
- The gitmoji must be the closest to the ones defined by https://gitmoji.dev and be in unicode.
- The test-emoji is either ✅ (succeeds) or 🚫 (fails) and is followed by the number of tests suceeding `<x>` and total tests `<y>`. If no tests were run do not write the line.
- Each body lines must wrap at 72 characters
- The trailer `Branch:` is required on each commit and is the current feature branch. MUST NOT be `main`
