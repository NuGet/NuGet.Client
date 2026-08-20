---
name: explain-pr
description: 'Explain a NuGet.Client pull request in a Markdown report. Use when asked to explain, summarize, or walk through a PR. Reads the PR, linked issue, diff, and surrounding code; explains the problem and solution; gives a real-world example; traces the end-to-end request flow through the changed files for a reader new to the repo; and analyzes each changed file.'
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

## Phase 10: Trace the End-to-End Flow

Write a formal trace of one concrete trigger (a user action, an API call, a CLI command, a scheduled job — whatever kicks off the changed code) through the system, file by file, in the order execution actually reaches them. This section is for a reader who has never seen this repository and does not know its architecture, but treats them as capable of following precise notation — prefer a rigorous, algorithm-like specification over a narrative told through analogies.

**Define the entities and variables first.** Before narrating the sequence, introduce the actors, inputs, and data objects involved, each as a short symbol plus a one-line definition, e.g.:
- `U` — the user or caller initiating the request (with relevant attributes, e.g. `U.scopes`, `U.identity`)
- `F` — the input payload (e.g. the uploaded file/stream)
- `R` — the persistent record(s) read or created (e.g. `R_pkg`, `R_staged` for distinct records), naming the fields that matter (e.g. `R_pkg.status`)
- `S` — any storage/service the flow touches (e.g. `S_blob`, `S_db`), where relevant
- Any policy/config value gating behavior (e.g. `flag(U)` for a feature flag lookup)

Use short, stable symbols consistently for the rest of the section rather than re-describing each object in prose every time.

**Then specify the flow as an ordered sequence of steps**, each written as a precise operation over these variables rather than a story. For each step:
1. Name the file (or small group of tightly related files, e.g. an interface + its implementation) responsible for that step.
2. State the step as a transformation or check, e.g. `validate(F) → error ∨ M` (validating the input either fails or yields extracted metadata `M`), `authorize(U, R_pkg) → allow ∨ deny(reason)`, `R_pkg.status: X → Y` for a state change, or `S_blob.write(path, F) → path`.
3. State the branch condition and both outcomes where the logic branches (success/failure, allowed/denied), not just the happy path.
4. Note any side effect precisely: what is written, to which variable/storage, and its new value — at the point it happens, not deferred to a later summary.

Rules for this section:
- Start from the outermost trigger a real user or system would cause (an HTTP call, a command, an event), not from an arbitrary internal function.
- Follow actual call order, not file-alphabetical or diff order. If file A calls into file B which calls into file C, present them A → B → C.
- Prefer compact expressions and arrows/notation (`→`, `⇒`, set/record notation) over long prose sentences; use prose only to gloss what a step means the first time a pattern appears.
- Do not paste code — the notation should abstract away syntax while remaining precise about what transforms into what.
- Group small mechanical/config files (constants, DI registration, test fakes) into the step of the file they support, rather than giving each its own numbered step, unless the file has an interesting independent role.
- End with a compact closing expression or equation-like summary of the whole flow (e.g. `stage(U, F) = validate(F) ∧ authorize(U) ∧ flag(U) → S_blob.write ∧ R_pkg.status←Staged ∧ R_staged.create`), called out distinctly (e.g. as a blockquote or fenced block), plus one sentence of plain-English gloss beneath it.

If the PR has multiple independent entry points (e.g. two new API endpoints), trace the primary/most significant one in full and briefly note, using the same variables, how the other(s) differ, rather than duplicating the full walkthrough.

---

## Phase 11: Map Storage and State Architecture

Identify every persistent storage backend the changed code reads from or writes to, and describe it as a state machine. This section answers "what storages exist, what lives where, and what makes data move from one place/state to another."

1. **Inventory the storage backends.** For each one touched by the diff or its immediate surrounding code, note:
   - Its kind (relational database table, blob/file storage container or folder, cache, queue, external service, in-memory config file, etc.)
   - What it holds, in plain English (e.g. "the metadata row for every package ever pushed," "the actual `.nupkg` bytes for packages that are public and downloadable")
   - Whether it existed before this PR or is newly introduced by it
   - Read the surrounding code (schema/migration files, storage-provider/constants files, DI wiring) to confirm the real name/purpose rather than guessing from a variable name.

2. **Identify the states.** Find the enum, status field, or equivalent that represents where a piece of data (e.g. a package, a request, a job) is in its lifecycle. List every possible state, including ones not touched by this PR, so the new state(s) can be placed in context.

3. **Build the state machine.** For each state, describe:
   - What triggers entry into that state (an API call, a background job, a validation result, an admin action)
   - Which storage backend(s) are written to or read from during that transition
   - What becomes visible/available/invisible as a result (e.g. "now appears in search," "blocked from public download," "reserved but not public")
   - What transitions are possible out of this state, and what triggers each

   Present this as a short ordered list or a simple diagram-style sequence (e.g. `Uploaded (temp storage) → Validating (DB row created) → Available (public blob + DB row) / FailedValidation (DB row only, blob discarded)`). Use a Mermaid `stateDiagram-v2` block if it meaningfully clarifies the transitions; otherwise plain prose/list is sufficient — do not force a diagram where a short list reads better.

4. **Highlight what this PR changes about the architecture**: new storage added, new states added, changed transition rules, or changed visibility rules for an existing state. Distinguish clearly between what already existed and what is new.

Ground every claim in the actual files read in Phase 6 — do not speculate about storage behavior that isn't evidenced in code, migrations, or config.

---

## Phase 12: Per-File Change Analysis

For each changed file, produce:
1. **File path** (relative to repo root)
2. **Change type**: Added / Modified / Deleted
3. **What changed**: Describe the specific modifications
4. **Why it changed**: Connect to the overall solution
5. **Notable patterns**: Anything worth highlighting (new abstractions, API changes, etc.)

Order files logically (core changes first, then tests, then config/build files).

---

## Phase 13: Write the Markdown Report

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

## Flow Walkthrough

## Storage and State Architecture

## Per-File Changes

## Limitations
```

Put the **Flow Walkthrough** section from Phase 10 immediately after **Surrounding Context**, followed by **Storage and State Architecture** from Phase 11, and only then the detailed **Per-File Changes** breakdown. The narrative sections build up context (request flow, then data lifecycle) before the reader dives into file-by-file detail.

Use the **Limitations** section to record missing issues, undocumented design rationale, unavailable files, or other gaps that affect the explanation.
