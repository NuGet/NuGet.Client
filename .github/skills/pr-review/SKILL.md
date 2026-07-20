---
name: pr-review
description: Reviews a NuGet/NuGet.Client pull request like a senior maintainer — sets up an isolated review workspace, builds and tests the affected projects, traces changes across files, and returns high-signal, severity-tagged findings with a merge verdict. Invoke explicitly with a PR number.
disable-model-invocation: true
---
# NuGet/NuGet.Client PR Review

Review pull requests the way NuGet/NuGet.Client's senior maintainers do.

**Requires shell access** (`git`/`dotnet`).

Work in four phases: **Set up, Understand and verify, Judge, and Report.**

---

## Set up (isolated workspace)

Never operate in the developer's active checkout — use an isolated cache so your `git`/`dotnet` commands never disturb their working tree.

1. **Cache path:** `%LOCALAPPDATA%\GitHubCopilot\ReviewSkill\NuGet.Client`
2. **Clone or update:** clone if missing, else `git -C {cache} fetch origin`.
3. **Get the PR:** `git -C {cache} fetch origin pull/{PR}/head:pr-{PR}` then `git -C {cache} checkout pr-{PR}`.
4. **Restore when done:** `git -C {cache} checkout -`.

### Build and debug — read the repo's own runbooks, don't guess
- Build/test/debug (incl. cross-platform): read **`docs/debugging.md`**, **`docs/cross-platform-debugging.md`**, and **`CONTRIBUTING.md`**.
- **Build and test ONLY the affected project(s), never the whole solution** — a full NuGet.Client build/test is very slow.
  Build the changed project and run just the test project(s) that cover the change.

---

## Understand and verify

1. **Scope by area** and read the matching repo guideline before reviewing those files:
   | Changed area | Read before reviewing |
   |---|---|
   | `src/NuGet.Core/**` restore/resolution/protocol | `docs/coding-guidelines.md` |
   | `src/NuGet.Clients/**` (VS/WPF) | `docs/ui-guidelines.md`, `docs/coding-guidelines.md` |
   | user-facing strings / `*.resx` | `docs/localizability.md` |
   | API / type design | `docs/design-review-guide.md` |
   | `test/**` | `docs/coding-guidelines.md` (test conventions) |

2. **Trace beyond the diff — mandatory for any behavior/correctness finding.**
   The bug usually lives in how a *caller* consumes the changed value, not in the diff.
   For every changed method whose **return value, branch, or side effect changes**:
   - Open its direct **callers and callees, across project/assembly boundaries** — a value that looks benign at the change site can violate an assumption a consumer in another assembly relies on.
   - Enumerate the behavioral cases the change can take and state which path each hits — the distinct input shapes, config/environment states, and success vs. empty/failure results the code can encounter.
   - A new `return null` / empty / short-circuit / early-exit is **not reviewed** until you have read what every consumer does with it.

3. **Verify, don't guess.** Run the affected test(s) or reproduce the path; otherwise cite the exact code path that proves the finding.
   **Never assert the *direction* of a behavior change from the diff alone.**
   If you can't confirm it, say so and flag it — do not clear a concern as a non-issue without tracing it.

---

## Judge (review priorities)

Hold each change to the guideline doc for its area (the *Understand and verify* table) — check the written rule, don't rely on memory.
Surface findings in this priority order:

1. **Correctness and tests** (most frequent) — a behavior change must ship a test that *fails without the fix*; verify boundaries and every consumer of a new early-exit (see *Understand and verify*).
2. **Performance**
3. **API surface** — treat public API / `PublicAPI.*.txt` as a permanent commitment; otherwise per `design-review-guide.md`.
4. **Conventions** — rules per `coding-guidelines.md`.
5. **VS/IDE** (`src/NuGet.Clients/**`) — per `ui-guidelines.md`.
6. **Localization / telemetry / maintainability** — per `localizability.md` and `coding-guidelines.md`.
7. **Process hygiene** — focused scope; PR template with a linked `Fixes:` issue; tracking issue for deferred work.

### Severity
- **[blocking]** — correctness bugs; a missing or ineffective test for a behavior change; or an unverified behavior change on a hot path (defaults to **[blocking]** until a test pins the behavior or the author confirms intent).
- **[suggestion]** — Need to be addressed before merge, but not a correctness bug. Examples: performance, API surface, conventions, VS/IDE, localization, telemetry, maintainability, or process hygiene.
- **[nit]** — does not need to be addressed before merge, but would improve the PR. Examples: style, naming, comments, or minor refactoring.

---

## Report

For each finding:
```
[severity] path/to/File.cs:Line — <one-line problem>
Why: <impact in 1 sentence>
Suggest: <concrete fix>
```
Then a **verdict**:
- **Request changes** if any [blocking] finding exists, **or if an unresolved behavior-change question remains open** (never "Approve" while a behavior change is unverified).
- **Approve with suggestions** if only [suggestion]/[nit].
- **Approve** if clean.

Be concise, cite `file:line`, give an actionable fix for every finding, and state which projects you built and which tests you ran.
