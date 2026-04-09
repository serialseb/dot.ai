---
name: git-commit
description: Use when committing files to git. It describes format and content to include in the commit messages that MUST be followed.
compatibility: Requires git
metadata:
  tags: [git, commit, gitmoji, plan, task]
---

# git commits (MUST)

- Merge commits are forbidden.
- All work MUST be done on feature branches unless specifically allowed by the user with branch-name being (`f/<plan-name>`)

## Forbidden

Unless otherwise requested, you can only write in non-destructive manners (git stash,git commits, git workspace, but not git push, git rebase, git cherry-pick). If you need those, ASK the user.

## Style

- The `<subject>` must describe the change in imperative form, e.g. "Encapsulate class X". Avoid starting words with too generic meaning like "add".
- The `<subject>` always starts with a capital letter and never ends with punctuation  
- Both the subjet and the body MUST explain the WHY not the HOW. Avoid technical details and if absolutely ncesessary keep then short.

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
- The gitmoji must be the closest to the ones defined by https://gitmoji.dev and be in unicode
- The test-emoji is either ✅ (succeeds) or 🚫 (fails) and is followed by the number of tests suceeding `<x>` and total tests `<y>`. If no tests were run do not write the line.
- Each body lines shoud wrap at 72 characters
- The trailer `Branch:` is required on each commit and is the current feature branch. MUST NOT be `main`
