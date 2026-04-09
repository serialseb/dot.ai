---
name: dotnet-coding
description: Use for any .NET or C# code change: implement, edit, modify, refactor, fix, add, remove, rename, or update code, tests, or project files. Trigger for coding work in .NET repositories without requiring the user to name the skill.
metadata:
  tags: [dotnet, csharp, coding, code-edit, implementation, refactor, fix, tests]
---

# Dotnet Coding

Use this skill for implementation work in `.NET` repositories, especially when changing `C#` code, tests, or project files.

## Defaults

- Target `.NET 10` and `C# 14` or newer
- Follow `SOLID` and `KISS`
- Prefer composable components
- Prefer functional style where it fits naturally
- Use the latest clear syntax the file already supports
- Write only enough code to fulfill the feature
- Do not add speculative guards for unlikely events
- Do not catch and swallow exceptions
- Prefer self-descriptive code over comments
- Follow normal naming conventions
- Match the style of the file you are editing

## Editing Rules

- Keep changes local unless broader change is required
- Do not change architecture without permission
- Reuse existing patterns before introducing new ones
- Keep public APIs and behavior stable unless the task requires a change
- In test projects, follow the test project's existing conventions

## Stop

Stop when the requested change is complete and verified enough for the current task.
