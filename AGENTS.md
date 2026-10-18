# Rule Selection
Before changing or reviewing repository files, identify every target file, including files to be added. Paths below are relative to the repository root.

Read each matching rule file in full before starting work. If multiple rules match, read all of them. If it is uncertain whether a rule applies, read the rule. The selected rules take precedence over general programming knowledge.

## Rules That Apply to Every Target File
- `.claude/rules/commands.md` (`**/*`) - Read before changing or reviewing any repository file and before running build, test, format, benchmark, or repository scripts.
- `.claude/rules/documentation-style-and-git.md` (`**/*`) - Read before changing or reviewing any repository file. It also applies to documentation, comments, commit messages, branches, changelogs, and pull requests.

## Source Code Rules
- `.claude/rules/csharp-standards.md` (`src/**/*.cs`) - Read when changing or reviewing C# source files under `src/`.
- `.claude/rules/performance-and-aot.md` (`src/**/*.cs`) - Read when changing or reviewing C# source files under `src/`.
- `.claude/rules/safety-and-nullability.md` (`src/**/*.cs`) - Read when changing or reviewing C# source files under `src/`.

## Test Rules
- `.claude/rules/testing.md` (`tests/**/*.cs`) - Read when changing or reviewing C# test files.

## Development Guidelines
- `docs/development.md` - Read when changing or reviewing architecture, dependencies, project structure, or build setup.
- `docs/development_guidelines.md` - Read when changing or reviewing public APIs, coding conventions, or development workflow.
