# dotai 0.1 — Design

A small CLI for sharing AI agent skills and top-level project files across repositories through symlinks into a cloned source repository.

## Goals

- Share skills across multiple projects by cloning one or more source repositories and symlinking their contents into each consumer project.
- Support multiple AI agent layouts (`.claude`, `.codex`, `.opencode`) by creating per-agent symlinks.
- Support top-level project files (configs, docs) delivered by symlink so edits flow back to the source repository.
- Provide `init` and `sync` commands with a minimal config file.

Non-goals (0.1):
- Installation of dotai itself. Handled outside the tool.
- Cross-platform binary distribution. Single arch for current user.
- Authentication configuration, SSH remotes, private repo tokens.
- Preservation of JSONC comments on write.

## Language and runtime

C# on .NET 10 with Native AOT. Single self-contained executable. No reflection at runtime — JSON via `System.Text.Json` source-generated contexts.

Reasons:
- Project handles structured data (multiple repos, multiple agents, config) that is awkward in bash 3 (no associative arrays).
- AOT binary is small (~1 MB) and startup is indistinguishable from a shell script.
- Developer is .NET-fluent; debugging and testing are better.

Dependencies:
- `git` CLI at runtime (shelled out for all git operations).
- No NuGet packages beyond the BCL.

## Project layout

```
src/Dotai/
  Dotai.csproj
  Program.cs
  Commands/
    ICommand.cs
    InitCommand.cs
    SyncCommand.cs
    HelpCommand.cs
  Services/
    ConfigStore.cs
    GitClient.cs
    AgentDetector.cs
    SkillLinker.cs
    GitignoreWriter.cs
  Ui/
    Robot.cs
    Console.cs
src/Dotai.Tests/
  Dotai.Tests.csproj
  ConfigStoreTests.cs
  AgentDetectorTests.cs
  GitignoreWriterTests.cs
  InitCommandTests.cs
  SyncCommandTests.cs
  Fixtures/
    LocalGitRepo.cs
```

### csproj flags

```xml
<TargetFramework>net10.0</TargetFramework>
<PublishAot>true</PublishAot>
<InvariantGlobalization>true</InvariantGlobalization>
<StackTraceSupport>false</StackTraceSupport>
<UseSystemResourceKeys>true</UseSystemResourceKeys>
<DebuggerSupport>false</DebuggerSupport>
<EventSourceSupport>false</EventSourceSupport>
<MetadataUpdaterSupport>false</MetadataUpdaterSupport>
<HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>
<Nullable>enable</Nullable>
```

## Command dispatch

`Program.Main(string[] args)` hand-rolled dispatch. No System.CommandLine dependency.

Routing:
- `[]` or `["--help"]` → `HelpCommand`
- `["init", ...]` → `InitCommand`
- `["sync", ...]` → `SyncCommand`
- `["<cmd>", "--help"]` → per-command help
- Unknown command → help with exit code 1

`ICommand`:
```csharp
interface ICommand {
    string Name { get; }
    string Help { get; }
    int Execute(string[] args);
}
```

## Repo root anchor

All project paths resolve relative to the **repo root**: the top-level directory of the enclosing git working tree, located by walking up from the current working directory until a `.git` directory (or file, for worktrees) is found. dotai always anchors to this directory regardless of the caller's repo root. This means there is exactly one `.ai/` per repository, never one per subdirectory. The term `<repo>` below refers to this path.

If no enclosing git repo is found, commands exit with `error: dotai requires a git repository`.

## Config file

Path: `<repo>/.ai/config.jsonc`

Shape:
```jsonc
{
  "https://github.com/<owner>/<repo>": {},
  "https://github.com/<owner2>/<repo2>": {}
}
```

- Keys: GitHub HTTPS URIs.
- Values: empty object, reserved for per-repo options in future versions.

Read: `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }`. Parse into `Dictionary<string, JsonElement>`. Comments are tolerated on read.

Write: serialize `Dictionary<string, JsonElement>` via source-generated context with 2-space indent. Comments are not preserved (documented limitation).

Source generator:
```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal partial class ConfigJsonContext : JsonSerializerContext { }
```

`ConfigStore`:
- `Load(string path)` → returns dict, creates empty dict if file absent
- `AddRepo(string uri)` → adds key with empty object value if not present; no-op if present
- `Save(string path, Dictionary<string, JsonElement>)` → writes file, creating parent dirs

## Git operations

`GitClient` shells to `git` via `System.Diagnostics.Process`. Each call takes a working directory and argv, returns exit code + captured stdout/stderr.

Operations used:
- `clone <uri> <path>`
- `status --porcelain`
- `add -A`
- `commit -m "<msg>"`
- `fetch origin`
- `symbolic-ref refs/remotes/origin/HEAD` (parse to extract default branch)
- `rebase origin/<branch>`
- `push origin <branch>`
- Rebase-in-progress detection: check `<repo>/.git/rebase-merge/` and `<repo>/.git/rebase-apply/` existence

## Agent detection

`AgentDetector` scans repo root for hardcoded list:
- `.claude`
- `.codex`
- `.opencode`

Returns list of directories present. Does not create missing ones. Adding new agents is a code change, not a config change, for 0.1.

## Symlink layout

`SkillLinker` creates symlinks in two buckets.

### Skills

For each repo clone `<clone>` and each detected agent dir `<agent>`:
- Source: `<clone>/skills/<skill-name>/` (directory)
- Target: `<repo>/<agent>/skills/<skill-name>` (symlink to directory)

Uniform path across agents for 0.1. Per-agent mappings may land later; not configurable now.

### Files

For each repo clone `<clone>`:
- Walk `<clone>/files/` recursively.
- For each regular file at relative path `p` inside `files/`:
  - Source: `<clone>/files/<p>`
  - Target: `<repo>/<p>` (symlink to file)
  - Create any missing parent directories at target.

### Creation behavior

- Symlinks are absolute (target paths absolute). Simpler to reason about.
- If target symlink exists and points at the correct source → leave alone.
- If target symlink exists but points elsewhere → replace.
- If target exists as a regular file or directory (not a symlink) → skip this entry, record manual conflict.

### Name collisions across repos

If two registered repositories both expose a skill or file at the same relative path, dotai refuses to resolve the ambiguity:
- Track which clone each intended target path comes from during a sync pass.
- First repo in config-file order creates the symlink. Later repos encountering the same target path do not touch it and raise a loud error: both source paths listed, recorded in `needs_manual`, sync continues with remaining work.

### Orphan cleanup (dotai-owned only)

A symlink is dotai-owned iff its target resolves to a path inside any `<repo>/.ai/repositories/*/` directory. Orphan cleanup only ever touches dotai-owned symlinks. Symlinks pointing elsewhere are never inspected or removed.

After processing a clone (or when no push was needed):
- Walk each agent's `<agent>/skills/` dir. For every dotai-owned symlink whose target does not exist, remove it.
- Walk the set of previously known `files/`-derived target paths (reconstructed by scanning `<clone>/files/`). For every dotai-owned symlink at a target path whose source file no longer exists in the clone, remove it. Parent dirs left in place.

## `.ai/.gitignore`

Content managed by `GitignoreWriter`:
```
repositories/
```

Behavior:
- If file absent: write.
- If present and line missing: append.
- If present and line present: no-op.

Purpose: consumer project's git does not track cloned source repos.

## `init` command

Signature: `dotai init <owner>/<repo>`

Flow:
1. Validate repo root is inside a git repo (walk up from repo root looking for `.git`). On failure exit 1 with `error: dotai requires a git repository`.
2. Validate arg matches `^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$`. On failure exit 1.
3. Derive URI `https://github.com/<owner>/<repo>` and clone dir name `<owner>_<repo>`.
4. Ensure `.ai/` and `.ai/repositories/` exist.
5. Update `.ai/.gitignore` via `GitignoreWriter`.
6. Load (or create) `.ai/config.jsonc`. Add URI key with empty object if missing. Save.
7. If `.ai/repositories/<owner>_<repo>/` absent, clone. If present, leave untouched (sync handles updates).
8. Run robot UI (~1 s, skipped if stdout is not a TTY).
9. Delegate to sync (in-process).

`init` can be called repeatedly with different `<owner>/<repo>` values to register multiple sources.

## `sync` command

Signature: `dotai sync` (no args in 0.1; operates over all configured repos).

Flow:
1. Load `.ai/config.jsonc`. If missing or empty, exit 1 with `error: no repositories configured (run dotai init first)`.
2. Detect agent dirs once.
3. For each configured repo, in config-file order:
   1. Set working dir to `<clone>`.
   2. If `.git/rebase-merge/` or `.git/rebase-apply/` exists → record as needs-manual, continue to next repo.
   3. `git status --porcelain` → if changes: `git add -A` then `git commit -m "dotai sync"`.
   4. `git fetch origin`.
   5. Resolve default branch via `git symbolic-ref refs/remotes/origin/HEAD`.
   6. `git rebase origin/<branch>`. On non-zero: leave rebase state in place, record needs-manual, continue.
   7. `git push origin <branch>`. On non-zero: record needs-manual, continue.
   8. Refresh symlinks for this clone (skills into each agent dir; files into repo root). Record any conflicts.
   9. Orphan-cleanup symlinks targeting this clone.
4. Print report. If `needs_manual` non-empty:
   - List each repo path with a brief reason (rebase in progress, push failed, symlink conflict).
   - Message: `resolve the issues above, then run 'dotai sync' again`.
   - Exit code 3.
5. Otherwise exit 0.

## Help text

`dotai` with no args or `--help`:

```
dotai — share AI skills and files across projects via symlinks

usage:
  dotai init <owner>/<repo>   register a source repository and sync
  dotai sync                  sync all configured repositories
  dotai --help                this message

config is kept in .ai/config.jsonc in the current project.
```

Per-command `--help` prints command description and usage line.

## Robot UI

`Ui/Robot.cs` holds a multi-line `const string` with ASCII art of a robot reading a book. On `init` success:
1. If `Console.IsOutputRedirected` → skip.
2. Print art.
3. `Thread.Sleep(1000)`.
4. Clear: `\x1b[2J\x1b[H`.

Sizing keeps the art under ~20 lines to fit common terminals.

## Exit codes

- `0` success
- `1` user error (not a git repo, bad argument)
- `2` system error (git command failed unexpectedly, symlink creation failed for a non-conflict reason)
- `3` partial success — one or more repos need manual intervention

## Error formatting

- `error: <short message>` on stderr for fatal errors.
- `warn: <short message>` on stderr for non-fatal.
- Plain text on stdout for information.
- No color in 0.1.

## Testing

xUnit project `Dotai.Tests`. Happy path only.

Cases:
- `ConfigStore` — add key to empty, add key to existing, idempotent, read tolerates comments.
- `GitignoreWriter` — create, append, idempotent.
- `AgentDetector` — finds existing dirs, ignores absent, handles no agents.
- `InitCommand` — against a temp repo root wired to a local bare git repo via `file://` URI: produces `.ai/` tree, config entry, clone dir, symlinks.
- `SyncCommand` — no-changes sync is a no-op; edit inside symlink produces commit and push; rebase-in-progress is reported.

Fixtures (`LocalGitRepo`) create a temp bare repo plus a populated working repo, returning a local URI. No network required.

## Out of scope for 0.1

- SSH URIs, private repo auth handling.
- Configurable agent list.
- Per-agent skill path overrides.
- Preserving JSONC comments on write.
- `dotai install` or bootstrap of the tool itself.
- Localized strings, globalization.
- Colored output.
- Partial sync (`dotai sync <owner>/<repo>`).
- Removal of a registered repository (`dotai remove` or similar).
