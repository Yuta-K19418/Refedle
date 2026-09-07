---
paths:
  - "**/*"
---

# Documentation Style & Git Policy

## Language Policy
- **Documentation**: All documentation (including README, specs, design docs, and TASKS.md) must be written in **English ONLY**.
- **Comments**: All code comments (inline `//` and XML documentation comments `///`) must be written in **English**.
- **Commit Messages**: Must be written in **English**.

## Comment Content Policy
- Keep comments to a maximum of **3 lines**. If a longer explanation is needed, write it as an XML documentation comment (`///`) or in project documentation instead
- **WHY, not WHAT**: Only write a comment when the reasoning is non-obvious — a hidden constraint, a subtle invariant, or a gotcha a reader would otherwise miss. Do not explain what the code already makes clear

## Git Workflow
- **ALWAYS** run `dotnet format` and `dotnet test` BEFORE committing.
- Ensure the project compiles with **Zero Warnings** (`TreatWarningsAsErrors` is enabled).
- Do **not** put a Claude session ID or session URL in commit messages (or PR descriptions). A `Co-Authored-By:` trailer is fine.

## Conventional Commits
Follow the **Conventional Commits** specification for all commit messages.

### Format
```
<type>: <description>
```

### Types
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that do not affect the meaning of the code (white-space, formatting, etc)
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `perf`: A code change that improves performance
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to the build process or auxiliary tools and libraries

### Example
```
feat: add SIMD-accelerated newline detection
```
