# GitSync

> A .NET global tool that keeps a git repository in sync — automatically pulling, committing, and pushing on a configurable schedule.

[![NuGet](https://img.shields.io/nuget/v/GitSync.svg)](https://www.nuget.org/packages/GitSync)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GitSync.svg)](https://www.nuget.org/packages/GitSync)
[![Build](https://github.com/yourorg/GitSync/actions/workflows/ci.yml/badge.svg)](https://github.com/yourorg/GitSync/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

---

GitSync watches a local git repository and, on every tick of a configurable loop, does three things:

1. **Pulls** the latest changes from the remote.
2. **Detects** any uncommitted local changes.
3. **Commits and pushes** them with a timestamped message — if anything changed.

Leave it running in the background during a work session, point it at a notes repo, a config directory, or any folder you want continuously backed up to a remote — and never worry about forgetting to push again.

---

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Usage](#usage)
- [Options](#options)
- [Examples](#examples)
- [How It Works](#how-it-works)
- [Building from Source](#building-from-source)
- [Project Structure](#project-structure)
- [Contributing](#contributing)
- [License](#license)

---

## Features

- 🔄 **Configurable poll interval** — defaults to every 1 second; set anything from 100 ms upward.
- ⏱ **Automatic session end** — runs for a configurable `--duration` or until a fixed `--end-time`; defaults to 8 hours.
- ✍️ **Custom commit messages** — supports a `{timestamp}` placeholder for automatic timestamping.
- 🛡 **Shell-injection safe** — commit messages and all git arguments are passed via `ArgumentList`, never a shell string.
- ⏳ **Subprocess timeout** — configurable `--git-timeout` prevents a hanging `git push` from blocking the loop forever.
- 🔁 **Resilient loop** — per-iteration errors are caught and logged; the loop never crashes on a transient failure.
- 🔍 **Structured logging** — powered by `Microsoft.Extensions.Logging` with configurable log level.
- 📦 **Zero-dependency install** — ships as a .NET global tool; one `dotnet tool install` and it's on your PATH.

---

## Requirements

| Requirement | Version |
|---|---|
| .NET SDK / Runtime | 8.0 or later |
| `git` | Any version on system `PATH` |

GitSync delegates all git operations to the locally installed `git` binary. Authentication (SSH keys, credential helpers, etc.) is handled by your existing git configuration — GitSync does not manage credentials.

---

## Installation

```bash
dotnet tool install -g GitSync
```

Verify the install:

```bash
gitsync --version
```

### Updating

```bash
dotnet tool update -g GitSync
```

### Uninstalling

```bash
dotnet tool uninstall -g GitSync
```

---

## Quick Start

Run with no arguments from inside a git repository. GitSync will pull, detect changes, commit, and push every second for up to 8 hours:

```bash
cd /path/to/your/repo
gitsync
```

That's it. Press **Ctrl+C** to stop at any time.

---

## Usage

```
gitsync [options]

Options:
  --path <path>               Path to the git repository  [default: current directory]
  --interval <timespan>       Delay between iterations    [default: 00:00:01]
  --duration <timespan>       Run for this long           [default: 08:00:00]
  --end-time <datetimeoffset> Stop at this absolute time  [mutually exclusive with --duration]
  --message <template>        Commit message template     [default: Auto-sync: {timestamp}]
  --log-level <level>         Logging verbosity           [default: Information]
  --git-timeout <timespan>    Per-command git timeout     [default: 00:00:30]
  --version                   Show version information
  -?, -h, --help              Show help and usage information
```

---

## Options

### `--path`

Path to the git repository to sync. Defaults to the current working directory.

```bash
gitsync --path ~/projects/my-notes
```

The path must exist and be a valid git repository. GitSync exits with code `1` if validation fails.

### `--interval`

How long to wait between iterations. Accepts any valid `TimeSpan` string (`HH:MM:SS` or `HH:MM:SS.fff`). Minimum is `00:00:00.100` (100 ms).

```bash
gitsync --interval 00:00:05      # every 5 seconds
gitsync --interval 00:05:00      # every 5 minutes
```

### `--duration` / `--end-time`

These options are **mutually exclusive** — provide one or neither.

- `--duration` — run for this long from the moment the tool starts.
- `--end-time` — stop at this absolute ISO 8601 date/time.
- If neither is given, the tool runs for **8 hours**.

```bash
gitsync --duration 01:00:00                     # run for 1 hour
gitsync --end-time 2026-04-03T18:00:00+01:00    # stop at 6 PM BST
```

### `--message`

Commit message template. The `{timestamp}` placeholder is replaced with the current UTC time in `yyyy-MM-ddTHH:mm:ssZ` format at the moment the commit is made.

```bash
gitsync --message "auto: {timestamp}"
gitsync --message "WIP checkpoint"
```

Default: `Auto-sync: {timestamp}`

### `--log-level`

Controls how much output GitSync produces. Valid values (case-insensitive): `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`.

```bash
gitsync --log-level Warning     # only warnings and errors
gitsync --log-level Debug       # verbose output including git stdout
```

Default: `Information`

### `--git-timeout`

Maximum time to wait for any single `git` subprocess to complete (e.g., `git pull`, `git push`). If the process does not exit within this window it is forcefully killed and the iteration is skipped.

```bash
gitsync --git-timeout 00:01:00   # allow up to 60 seconds per command
```

Default: `00:00:30`

---

## Examples

**Back up an Obsidian vault every 30 seconds for a workday:**

```bash
gitsync --path ~/vaults/work --interval 00:00:30 --duration 09:00:00
```

**Sync a config directory overnight, stopping at 6 AM:**

```bash
gitsync --path ~/.dotfiles --end-time 2026-04-04T06:00:00Z --interval 00:01:00
```

**Use a custom commit message with a project prefix:**

```bash
gitsync --message "chore(auto): checkpoint {timestamp}"
```

**Run quietly in CI — only log warnings and errors:**

```bash
gitsync --path /workspace --log-level Warning --duration 00:30:00
```

**Debug a connectivity issue — verbose output, short timeout:**

```bash
gitsync --log-level Debug --git-timeout 00:00:10
```

---

## How It Works

Each iteration of the loop runs the following pipeline in sequence:

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────────┐     ┌──────────┐
│  git pull   │────▶│ git status       │────▶│ git add -A          │────▶│ git push │
│             │     │ --porcelain      │     │ git commit -m "..."  │     │          │
└─────────────┘     └──────────────────┘     └─────────────────────┘     └──────────┘
                           │ empty?
                           ▼
                    (skip — no changes)
```

1. **Pull** — fetch and merge the latest remote changes.
2. **Detect** — run `git status --porcelain`; if output is empty, skip to the next iteration.
3. **Stage** — `git add -A` stages all tracked and untracked changes.
4. **Commit** — create a commit with the resolved message template.
5. **Push** — push to the tracked upstream. Falls back to `git push origin HEAD` if no upstream is configured.
6. **Wait** — sleep for the configured `--interval` before the next iteration.

Any step that fails is logged and the *remainder of that iteration* is skipped. The loop continues — a single bad iteration never terminates the session.

---

## Building from Source

```bash
# Clone
git clone https://github.com/yourorg/GitSync.git
cd GitSync

# Build
dotnet build

# Run locally
dotnet run -- --help

# Pack as NuGet tool
dotnet pack -c Release -o ./artifacts

# Install the locally-built package
dotnet tool install -g GitSync --add-source ./artifacts
```

---

## Project Structure

```
GitSync/
├── Commands/
│   └── SyncCommand.cs              # RootCommand — all CLI options and handler
├── Services/
│   ├── ISyncLoopOrchestrator.cs    # Loop engine interface
│   ├── SyncLoopOrchestrator.cs     # Loop implementation
│   ├── IGitService.cs              # Git operations interface
│   ├── GitService.cs               # Git operations implementation
│   ├── IGitProcessRunner.cs        # Subprocess execution interface
│   └── GitProcessRunner.cs         # Subprocess implementation (timeout + injection safety)
├── Models/
│   ├── SyncOptions.cs              # Immutable options DTO
│   ├── GitCommandResult.cs         # Subprocess result record
│   ├── GitCommandOptions.cs        # Subprocess input record
│   └── IterationOutcome.cs         # Per-iteration outcome enum
├── Infrastructure/
│   └── ServiceCollectionExtensions.cs  # DI registration
├── Program.cs                      # Entry point
└── GitSync.csproj                  # PackAsTool, NuGet metadata
```

Each `System.CommandLine` `Command` subclass lives in its own file under `Commands/` — new commands can be added without touching existing ones.

Full design documentation is in [`docs/detailed-designs/`](docs/detailed-designs/00-index.md).

---

## Contributing

Contributions are welcome! Please open an issue before submitting a pull request for significant changes.

1. Fork the repository.
2. Create a feature branch: `git checkout -b feat/my-feature`.
3. Commit your changes following [Conventional Commits](https://www.conventionalcommits.org/).
4. Push and open a pull request against `main`.

Please ensure the project builds and all tests pass before submitting:

```bash
dotnet build
dotnet test
```

---

## License

GitSync is released under the [MIT License](LICENSE).
