---
name: explain-pr
description: 'Produce a self-contained Markdown explanation of a NuGet.Client pull request. Use when asked to explain, summarize, or walk through a PR. Reads the PR, linked issues and comments, diff, tests, and surrounding code; explains the problem with a real-world example; describes the solution; and analyzes every changed file in logical review order.'
---

# Explain a NuGet.Client pull request

Create a detailed, self-contained explanation that lets a reviewer understand a pull request without first reading its issue, description, and raw diff.

This is an explanation skill, not an approval or defect-finding skill. Do not give a merge verdict. Use the `pr-review` skill when the user requests correctness findings or an approval decision.

## Inputs and output

Accept a NuGet.Client PR URL or number. If neither is supplied, use the PR associated with the current branch when one exists. Otherwise ask which PR to explain.

Write the complete report to:

- PR number available: `.pr-explanations/pr-<number>.md`
- Local branch without a PR: `.pr-explanations/<sanitized-branch-name>.md`

The `.pr-explanations` directory is git-ignored. Create it when needed and overwrite an existing report for the same PR so the result is current. Do not commit generated reports.

Sanitize branch names by replacing characters other than letters, numbers, dots, underscores, and hyphens with `-`.

After writing the report, return its repository-relative path and a short summary in chat. Do not duplicate the report in chat unless requested.

## Rules

- Separate facts found in the PR, issue, diff, and code from your own inference.
- Never invent motivation, rejected alternatives, measured results, or user impact.
- Call out missing context rather than filling it with assumptions.
- Explain code in plain English before using implementation terminology.
- Read complete changed files and enough surrounding code to understand contracts and behavior.
- Analyze every changed file, including tests and build files, but keep generated or mechanical changes concise.
- Cite repository-relative paths and symbols. Include changed line numbers where they improve navigation.
- Organize the report by how a reviewer should understand the change, not alphabetically.

---

## Phase 1: Resolve the PR

Resolve the requested PR and record:

- PR number and URL
- Title and author
- Base and head branches and commit SHAs
- State, labels, and milestone

For a GitHub PR, prefer GitHub tools for metadata and use `git` for repository contents. Fetch the PR head into an isolated ref such as `pr-<number>` without changing or modifying the user's working tree.

If explaining local changes, determine the intended base branch and include committed, staged, and unstaged changes that the user asked to explain.

---

## Phase 2: Read the PR Description

Read the complete PR body and extract:

- The stated problem
- The proposed solution
- Linked issues, design documents, and related PRs
- Claimed testing or performance results
- Rollout, compatibility, and documentation notes
- Explicit reviewer guidance

Treat these as the author's claims until confirmed by the diff, tests, or linked issue. Note contradictions or important omissions.

---

## Phase 3: Inspect Commits and Changed Files

Read the commit list and changed-file summary.

Use them to identify:

- The progression of the implementation
- Core product changes
- Tests
- Public API or persisted-format changes
- Configuration and build changes
- Generated or mechanical changes

Do not assume commit messages are accurate when they conflict with the final diff.

---

## Phase 4: Read Repository Guidance

Read the guidance relevant to the changed areas:

- New features or behavior changes: `docs/feature-guide.md`
- Public API changes: `docs/nuget-sdk.md`
- C# implementation: `.github/agent_docs/csharp.md`
- Localized resources: `.github/agent_docs/localization.md`
- Performance claims: `.github/agent_docs/benchmarking.md`

Use this guidance to explain NuGet-specific design constraints. Do not turn this phase into a style audit.

---

## Phase 5: Read the Complete Diff

Read the complete base-to-head diff, including renames and deletions. Then read the full head revision of every behaviorally important changed file.

Identify:

- Behavior that existed before the PR
- The exact point where the new behavior diverges
- New and changed APIs, data structures, conditions, and side effects
- Tests added or changed
- Changes that are only formatting, generated output, or mechanical wiring

Do not explain behavior from an isolated hunk when surrounding methods or types affect its meaning.

---

## Phase 6: Read Linked Issues

Use GitHub tools to fetch every directly linked issue:

- Read the issue body and all comments.
- Identify the reported problem, reproduction steps, and expected behavior.
- Note design decisions, constraints, rejected approaches, and unresolved questions.
- Distinguish the original report from conclusions reached later in discussion.

If no issue is linked, use the PR description as the sole source of problem context and state this limitation in the report.

---

## Phase 7: Understand the Problem

Synthesize information from:

- The linked issue
- The PR description
- The old code and the base-to-head diff

Explain:

1. What was broken, missing, or suboptimal
2. Who is affected, such as end users, package authors, developers, Visual Studio users, or CI systems
3. What happens if the problem is not fixed
4. Which NuGet component or workflow owns the behavior

Write for a reader who has not seen the PR or issue. Use plain English and be specific.

If the PR is a refactoring or engineering-only change, explain the maintenance or contributor problem instead of inventing user-facing impact.

---

## Phase 8: Generate a Real-World Example

Create one concrete, realistic scenario that demonstrates the problem:

1. Describe a specific user, developer, or system action.
2. Show what happens before the change.
3. Show what should happen instead.
4. Explain how the difference matters.

Use exact inputs and outcomes found in issues or tests when available. Clearly label illustrative values as illustrative. Never invent benchmark numbers, frequencies, error messages, or scale claims.

For an internal refactoring, use a contributor scenario that demonstrates the maintenance cost or risk.

---

## Phase 9: Understand and Explain the Solution

Analyze the author's approach:

1. What strategy, abstraction, or design pattern does it use?
2. How does data or control flow through the new implementation?
3. What are the key structural changes?
4. Why does the approach solve the problem?
5. Why was this approach chosen over alternatives?

Answer the last question only when the issue, PR discussion, design document, code constraints, or established repository pattern provides evidence. Otherwise state that the rationale is not documented.

Explain the solution at a high level before discussing individual files. Add concise pseudocode, an input/output table, or a Mermaid flow diagram when it makes non-trivial logic easier to understand.

---

## Phase 10: Explore Surrounding Context

Before analyzing individual files, explore the broader codebase:

- Read callers and callees of changed behavior.
- Read referenced files that were not changed.
- Understand interfaces, base classes, contracts, and persisted formats implemented or consumed by the changed code.
- Compare the implementation with similar patterns elsewhere in NuGet.Client.
- Trace important behavior across project and assembly boundaries.
- Check how Visual Studio, `dotnet`, MSBuild, and `nuget.exe` surfaces reach the changed code when relevant.

Read files from the PR head revision so analysis does not accidentally mix base-branch and PR-branch contents. For example:

```powershell
git show pr-<number>:<file-path>
```

Use the equivalent base revision to confirm old behavior when needed.

This phase supplies context for explanation. Do not report speculative defects.

---

## Phase 11: Analyze Each Changed File

Analyze every changed file and include:

1. **File path** relative to the repository root
2. **Change type**: Added, Modified, Renamed, or Deleted
3. **What changed**: the specific modifications
4. **Why it changed**: how it supports the solution
5. **Notable details**: new abstractions, contracts, behavior, API changes, or non-obvious implementation choices
6. **Reviewer focus**: the main question a reviewer should answer in this file

Order files logically:

1. Core behavior and data structures
2. Callers, wiring, and integration
3. Public API and persisted formats
4. Tests
5. Configuration, build, generated, and mechanical files

Group closely related files when explaining them together improves comprehension, but still account for each path explicitly.

---

## Phase 12: Explain Tests and Validation

Connect important behavior to evidence:

| Behavior or scenario | Test file and test | What it proves | Remaining gap |
|---|---|---|---|

Distinguish:

- Tests added or changed by the PR
- Existing tests that cover the path
- Manual validation claimed in the PR description
- Validation you actually performed

Do not equate a passing test project with coverage of a specific behavior.

---

## Phase 13: Identify Review Focus Areas

Summarize the areas that deserve reviewer attention without turning them into unproven findings:

- Behavior and edge cases
- Compatibility and feature gating
- Public APIs and persisted formats
- Performance-sensitive paths
- Error handling, diagnostics, and localization
- Test coverage

For NuGet changes, consider when relevant:

- `SdkAnalysisLevel` and feature flags
- PackageReference, packages.config, and central package management
- Restore no-op, lock files, and static graph restore
- Settings and configuration precedence
- Cross-platform and target-framework behavior
- Cancellation, concurrency, caching, ordering, and fallback behavior

Each focus area must cite the code path or change that makes it relevant.

---

## Phase 14: Write the Report

Write `.pr-explanations/pr-<number>.md` using this structure:

```markdown
# PR <number>: <title>

- **PR:** <url>
- **Author:** <author>
- **Base:** <base branch and SHA>
- **Head:** <head branch and SHA>
- **Generated:** <date>

## Executive summary

## Problem

## Real-world example

## Solution

## How it works

## Surrounding code and design context

## Changed files

## Tests and validation

## Review focus areas

## Suggested review order

## Context limitations
```

The executive summary should be understandable on its own. The suggested review order should give the shortest logical path through the changed files and state what the reviewer should learn from each stop.

Omit empty optional sections, but always include context limitations when an issue, design rationale, test evidence, or another important source was unavailable.
