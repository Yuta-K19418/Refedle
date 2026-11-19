---
paths:
  - "tests/Refedle.Tests/App/Tui/**/*.cs"
  - "tests/Refedle.IntegrationTests/App/Tui/**/*.cs"
  - "tests/Refedle.TestHelpers/App/Tui/**/*.cs"
---

# TUI Test Layers

How the TUI tests (`App/Tui`) are split into layers, and where each layer lives.
The shared conventions in [testing.md](testing.md) also apply. Helpers and doubles are
covered in [tui-test-helpers.md](tui-test-helpers.md).

## Scope
- These rules apply to the test paths listed in the front matter above. Other areas are not organized into layers yet; this file does not say how they should be tested.
- The layer model is not specific to the TUI. When another area is reorganized, add its test paths to the front matter instead of writing a second copy.

## Layers
Choose the layer by **what is checked**: one component's behavior, or several components working together. Whether Terminal.Gui is initialized or an event loop runs does not decide the layer.

| Layer | Subject | Collaborators | What it verifies | Project |
|---|---|---|---|---|
| Unit | One component | Test doubles or real objects | Pure logic, state-transition rules, behavior a caller can see (what is drawn, how it reacts, what it notifies). Never internal implementation | `Refedle.Tests` |
| Integration | Several components wired together | Real, connected to each other | The result of the wiring: view switching, what state shows on screen, flows that need a running UI loop (`LivePumpTestSession`) | `Refedle.IntegrationTests` |

- A test stays a unit test while it only checks the behavior of the one component under test, even when its collaborators are real. When components are connected and their behavior and state are checked together, it is an integration test.
- End-to-end tests (`Refedle.E2ETests`) are described in [e2e-testing.md](e2e-testing.md).

## Where tests live
- Both projects mirror the production folders. `src/App/Tui/UI/ModeController.cs` is tested by `App/Tui/UI/ModeControllerTests*.cs` in whichever project the layer says.
- The namespace matches the folder and starts with the project name, for example `Refedle.Tests.App.Tui.UI` and `Refedle.IntegrationTests.App.Tui.UI`.
- When one class has tests in two layers, keep the same class name in both projects. The project already says the layer, so do not add a layer suffix.

## Organizing test classes
- One test class covers one class under test.
- Split a large class into partial files **with the same class name**, one file per group of members under test (`AppStateTests.DrillDown.cs`). Splitting never changes test names.
- Fields, the constructor, `Dispose`, and helpers used by more than one partial file stay in the base file (`AppStateTests.cs`). A partial file never defines something another partial file needs.
