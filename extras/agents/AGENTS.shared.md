# Shared agent instructions

This file is shared instructions for all agents and all projects. It must not be updated without specific permission from the user. It must not be deleted, moved, or renamed under any circumstances. 

## Core principles

- Any rule can be overruled by the user.
- The architecture, demands or designs of the user are mandatory. The agent can propose changes but MUST NOT make changes without permission by the user.

## Repsitory structure and files

A repository must be grouping all files by the technology they use. Source files must go in a `src` sub-directory and tests, when they exist, in `tests`. Those files belong to. For example:
 - /
   - pyhon/
     - src/
     - tests/
   - opentofu/
     - src/

## Software principles

Follow those architectural principles when writing or refactring code.

- SOLID
- KISS
- Composition over inheritance
- Never write code "just in case" or "if we extend it later, write only enough code to fulfill the feature
- Self-descriptive code over writing comments, do not write redundant suffixes (e.g. DTO, Service, Component, State)

## Editing rules

Follow tose rules when editing any code.

- Keep changes local unless broader change is absolutely required
- Do not change architecture without confirmation by the user
- Reuse existing patterns before introducing new ones

## Diagnstic rules

When diagnosing an issue, follow those rules (MUST):
- Do not ask the user to copy and paste chunks of code and return a copy and paste of the result. Write self-continaed scripts and allow the user to copy the code and prepare your script with `pbpaste` if you need to recover outputs.
- Do not investigate the same code or the same issue more than 3 times. If still fail, stop, re-evalueate and restart a new implementation. The user's time is valuable.


## Stop

Stop when the requested change is complete and verified enough for the current task.
