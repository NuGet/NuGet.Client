---
name: explain-pr
description: 'Explain a NuGet.Client pull request in a Markdown report. Use when asked to explain, summarize, or walk through a PR. Reads the PR, linked issue, diff, and surrounding code; explains the problem and solution; gives a real-world example; and analyzes each changed file.'
---

# Explain a Pull Request

Create a self-contained explanation of a NuGet.Client pull request for a reviewer who has not read the PR or linked issue.

## Output

Write the final report to `.pr-explanations/pr-{PR}.md`. Create `.pr-explanations` if it does not exist and overwrite an older report for the same PR.

Do not commit the generated report. When finished, tell the user where the report was written.

---

## Phase 1: Fetch PR Metadata

Use GitHub MCP tools to fetch:
- PR number, title, author, and URL
- Base and head branches
- Labels and milestone
- Commit list
- Changed file list

Fetch the PR branch into a local `pr-{PR}` ref without modifying the user's working tree.

---

## Phase 2: Read PR Description

Read the complete PR description and identify:
- The problem stated by the author
- The proposed solution
- Linked issues and design documents
- Testing or validation described by the author

Treat these as claims until they are supported by the issue, diff, or tests.

---

## Phase 3: Read Linked Issue

Use GitHub MCP tools to fetch the linked issue(s):
- Read the issue body and all comments
- Identify the reported problem, reproduction steps, and expected behavior
- Note any discussion about design decisions or rejected approaches

If no issue is linked, use the PR description as the sole source of problem context. Note this limitation in the report.

---

## Phase 4: Identify Change Structure

Classify the changed files into:
- Core implementation
- Tests
- Public API
- Configuration or build files
- Generated or mechanical changes

Use this classification to determine the order in which to study the change.

---

## Phase 5: Analyze the Complete Diff

Read the complete base-to-head diff.

Identify:
- The behavior of the old code
- The behavior introduced by the PR
- The exact points where old and new behavior differ
- Tests added or changed for the new behavior

---

## Phase 6: Read Changed Files and Explore Surrounding Context

Read the complete PR version of every changed file, not only isolated diff hunks.

For deleted files, read the base-branch version. For renamed files, read both the old and new paths when needed to understand the change.

Before explaining the solution, explore the broader codebase:
- Read files that are referenced by changed code but not themselves changed
- Understand interfaces, base classes, and contracts that the changed code implements
- Check if the changes are consistent with patterns used elsewhere in the codebase

Use `git show` with the PR branch ref to read files:

```powershell
git show pr-{PR}:{file_path}
```

This is critical for detecting issues that only appear when you understand the full context.

---

## Phase 7: Understand the Problem

Synthesize information from:
- The linked issue (Phase 3)
- The PR description (Phase 2)
- The diff (Phase 5) — what was wrong with the old code?
- The surrounding code (Phase 6)

Produce a clear, concise explanation of:
1. What was broken, missing, or suboptimal
2. Who is affected (end users, developers, CI systems, etc.)
3. What the consequences are if the problem is not fixed

**Rules**:
- Write for a reader who has NOT seen the PR or issue
- Use plain English, not code
- Be specific — name the component, feature, or behavior affected

---

## Phase 8: Generate Real-World Example

Create a concrete, realistic scenario that demonstrates the problem. This should:
- Describe a specific user or system action
- Show what happens (the bug, failure, or suboptimal behavior)
- Show what should happen instead

**Good example**: "When a user restores packages for a solution with 50+ projects, the restore operation takes 45 seconds because dependency resolution iterates all projects sequentially. With the fix, parallel resolution reduces this to ~12 seconds."

**Bad example**: "The code had a performance issue that was fixed."

If the problem is purely internal (refactoring, code hygiene), describe the developer scenario instead (e.g., "A contributor adding a new package source would need to modify 3 files instead of 1").

Do not invent measurements or outcomes. When exact values are unavailable, use a realistic qualitative example or clearly label values as illustrative.

---

## Phase 9: Understand and Explain the Solution

Analyze the diff and surrounding code to understand the author's approach:
1. What design pattern or strategy did they choose?
2. Why this approach over alternatives?
3. What are the key structural changes?

Explain the solution at a high level before diving into per-file details.

If the reason for choosing this approach is not documented in the PR, issue, or codebase, say so instead of guessing.

---

## Phase 10: Per-File Change Analysis

For each changed file, produce:
1. **File path** (relative to repo root)
2. **Change type**: Added / Modified / Deleted
3. **What changed**: Describe the specific modifications
4. **Why it changed**: Connect to the overall solution
5. **Notable patterns**: Anything worth highlighting (new abstractions, API changes, etc.)

Order files logically (core changes first, then tests, then config/build files).

---

## Phase 11: Write the Markdown Report

Write `.pr-explanations/pr-{PR}.md` with:

```markdown
# PR {PR}: {title}

- **PR:** {url}
- **Author:** {author}
- **Base:** {base branch}
- **Head:** {head branch}

## Summary

## Problem

## Real-World Example

## Solution

## Surrounding Context

## Per-File Changes

## Limitations
```

Use the **Limitations** section to record missing issues, undocumented design rationale, unavailable files, or other gaps that affect the explanation.
