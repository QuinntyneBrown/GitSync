# Detailed Designs — Index

| # | Feature | Status | Description |
|---|---------|--------|-------------|
| 01 | [Application Bootstrap & CLI Structure](01-app-bootstrap/README.md) | Draft | Entry point, System.CommandLine command-per-file pattern, DI composition, NuGet packaging metadata, and startup validation sequence. |
| 02 | [Sync Loop Engine](02-sync-loop/README.md) | Draft | Timed polling loop with configurable interval and end time, per-iteration error isolation, graceful Ctrl+C cancellation, and commit message template resolution. |
| 03 | [Git Operations Service](03-git-operations/README.md) | Draft | Git executable discovery, subprocess execution with injection-safe `ArgumentList`, per-command timeout enforcement, and the full pull → detect → stage → commit → push workflow. |
