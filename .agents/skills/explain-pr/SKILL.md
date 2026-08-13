---
name: explain-pr
description: 'Explain a NuGet.Client pull request so reviewers can quickly understand its intent, behavior changes, important execution paths, risks, and test coverage. Use when asked to explain, summarize, walk through, or help review a PR. Produces a reviewer brief, behavioral walkthrough, risk matrix, suggested review order, and evidence-backed draft comments without making an approval decision.'
---

# Explain a NuGet.Client pull request

Make a pull request easy to understand and judge. Organize the explanation by reviewer value rather than file-tree order.

This skill prepares a reviewer to review the change. It does not replace the `pr-review` skill's verification, findings, or merge verdict.

## Inputs

Accept a NuGet.Client PR URL or number. If neither is supplied, use the PR associated with the current branch when one exists. Otherwise ask which PR to explain.

## Principles

- Explain why and how behavior changes, not merely which lines changed.
- Distinguish evidence from assumptions and questions.
- Focus on code added or modified by the PR, while reading callers and callees needed to explain its effects.
- Do not infer intent when the PR description, linked issue, and implementation disagree; surface the discrepancy.
- Do not manufacture risks or comments to make the output appear thorough.
- Do not approve, request changes, or post comments. Draft comments only when they help the reviewer act.
- Treat generated and mechanical changes as supporting material unless they affect behavior or compatibility.

## 1. Gather context

1. Resolve the PR and determine its base and head commits.
2. Read the PR title, description, labels, commits, changed-file list, and complete diff.
3. Read the linked NuGet/Home issue and any linked design document that explains intended behavior.
4. Read the full contents of behaviorally important changed files.
5. Trace the direct callers, callees, and relevant tests for changed behavior.
6. Read applicable repository guidance:
   - New features or behavior changes: `docs/feature-guide.md`
   - Public API changes: `docs/nuget-sdk.md`
   - C# implementation: `.github/agent_docs/csharp.md`
   - Localized resources: `.github/agent_docs/localization.md`
   - Performance claims: `.github/agent_docs/benchmarking.md`
7. Classify changed files as core behavior, wiring/integration, tests, public surface, or mechanical/generated.

Perform the initial explanation before reading existing review comments so they do not bias the mental model. Existing comments may be consulted afterward to identify already-resolved questions or useful context.

## 2. Build the mental model

Explain the change in this order:

1. **Purpose**: the user or engineering problem and intended outcome.
2. **Before and after**: externally observable behavior that changes.
3. **Execution path**: how control or data flows through the important components.
4. **Change map**:
   - Core behavior
   - Wiring, configuration, and integration
   - Public API, protocol, or persisted format
   - Tests
   - Mechanical or generated changes
5. **Affected NuGet surfaces**:
   - `dotnet restore` and MSBuild restore
   - Visual Studio
   - `nuget.exe`
   - Pack
   - NuGet SDK and public libraries

State that a surface is unaffected only when the code path or project boundaries provide evidence. Otherwise omit it.

## 3. Explain difficult logic

For dense logic, add only the aids that improve comprehension:

- Short pseudocode that removes language and error-handling noise.
- A small before/after trace using a realistic input.
- A state or call-flow diagram when ordering or component interaction matters.
- An input/output table when boundary behavior is the main concern.

Show where old and new paths diverge and the resulting observable effect. Do not mirror straightforward code in prose.

## 4. Highlight compatibility and risk

Check the areas relevant to the diff:

- `SdkAnalysisLevel`, feature flags, and default behavior
- PackageReference, packages.config, and central package management
- Settings, environment variables, and configuration precedence
- Error codes, messages, warnings, and localization
- Public APIs and `PublicAPI.*.txt`
- Cross-platform and target-framework behavior
- Concurrency, caching, cancellation, ordering, and fallback behavior
- Restore no-op, lock files, static graph restore, and persisted state
- Performance-sensitive restore, protocol, and Visual Studio paths

For each risk, cite the concrete file, symbol, or execution path that makes it relevant. Label uncertainty as a question, not a defect.

## 5. Connect behavior to tests

Build a table:

| Behavior or risk | Test evidence | Coverage gap |
|---|---|---|

Separate:

- Tests visible in the diff
- Existing tests that cover the path
- Validation claimed in the PR description but not independently visible

Explain what each important test proves. Do not equate the presence of a test file with coverage of the changed behavior.

## 6. Write the reviewer guide to Markdown

Always save the complete reviewer guide as a Markdown file under the repository's git-ignored `.test` directory:

- PR number available: `.test/pr-explanations/pr-<number>.md`
- Local branch without a PR: `.test/pr-explanations/<sanitized-branch-name>.md`

Create `.test/pr-explanations` when it does not exist. Sanitize branch names by replacing characters other than letters, numbers, dots, underscores, and hyphens with `-`.

The file must:

- Begin with `# PR <number>: <title>` for a PR, or `# <branch-name> PR explanation` for a local branch.
- Include the PR URL, base and head references, and generation date immediately below the title.
- Contain the complete output structure below.
- Use repository-relative file links where useful.
- Be overwritten when rerunning the explanation for the same PR or branch so stale results are not mistaken for current analysis.

Do not commit the generated explanation. After writing it, return the repository-relative output path and a brief reviewer summary in chat. Do not duplicate the complete guide in chat unless the user asks.

Use the following structure in the Markdown file. Omit empty sections.

### Reviewer brief

In at most five sentences, summarize the purpose, approach, affected surfaces, largest review risk, and test strategy.

### Behavioral walkthrough

Describe before/after behavior and the important execution path. Include pseudocode, a trace, diagram, or table only when useful.

### Change map

Group changes by reviewer value, not path order:

1. Core behavior
2. Wiring and integration
3. Public or persisted surface
4. Tests
5. Mechanical or generated changes

### Risk and compatibility matrix

| Area | What changed | What the reviewer should verify |
|---|---|---|

### Test evidence

Include the behavior-to-test table and meaningful coverage gaps.

### Suggested review order

Give the shortest ordered list of files or symbols needed to understand the change. For each entry, state what decision the reviewer can make there.

### Possible comments

Draft comments only for concrete findings or useful questions. Each comment must:

- Be labeled **Finding** or **Question**.
- Cite a changed file and line when possible.
- Explain why the point matters.
- Be concise enough to post as an inline review comment.

Do not draft style-only comments unless they materially affect comprehension or maintenance.

## Handoff to deeper review

If the reviewer asks for correctness findings, targeted verification, a merge verdict, or comments suitable for submission, invoke the `pr-review` skill using the context established here.
