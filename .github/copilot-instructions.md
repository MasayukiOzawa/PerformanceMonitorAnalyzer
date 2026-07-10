# Copilot Instructions

## Language

- Respond in Japanese by default.
- Summaries, pull request notes, and review comments should also be written in Japanese unless the user asks otherwise.

## Branches

- Always create and switch to a working branch before changing files.
- Do not edit, commit, or otherwise perform implementation work directly on `main` or `master`.
- Use `codex/*` for Codex-authored work and `feature/*` for ordinary feature branches.
- Do not create release automation unless the task explicitly asks for it.

## Project Facts

- This is a Windows WPF application targeting .NET 10.0.
- The solution is `src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln`.
- The app parses BLG files with the Windows PDH API.
- `relog.exe` is used as a reference command display; app data loading is PDH API based.
- Public sample BLG files are not tracked. Keep local `.blg` files under `sample/` out of Git.
- Build outputs, publish artifacts, logs, and local JSON exports must remain untracked.

## Build And Test

- Restore: `dotnet restore src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln`
- Debug build: `dotnet build src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln -c Debug`
- Tests: `dotnet test src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.sln`
- Release single-file artifacts are created by `publish.bat` or Release publish commands documented in `.github/instructions/build.instructions.md`.

## Documentation

- Keep `README.md` and `wiki/` aligned with the implemented UI and repository structure.
- Do not document files, setup paths, or menu entries that are not present in the repository.
- Update docs when user-facing behavior, build steps, dependencies, or release outputs change.

## UI Changes

- Preserve the existing WPF style resources in `src/PerformanceMonitorAnalyzer/Styles/`.
- When changing visible UI, verify text fit and capture a screenshot for PR notes when possible.
