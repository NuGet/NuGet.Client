---
name: pr-review-skill-genrator
description: You are a temporary skill builder. Your job is to analyze historical NuGet PRs and generate one final `pr-review.md` skill that can help reviewers produce useful, NuGet-specific PR review comments. This builder skill is temporary and should not be committed permanently.

---

# Temporary NuGet PR Review Skill Builder


## Goal

Create a PR review skill that captures reusable reviewer judgment from historical NuGet PRs.

The final skill should help review future NuGet PRs by identifying meaningful issues, asking useful questions, and suggesting tests or improvements.

## Important Rule

Do not copy old review comments directly.

Instead, extract the reusable reviewer rule behind each useful comment.

Ask:

```text
What knowledge, concern, or pattern allowed the reviewer to make this comment?
```

## Inputs To Analyze

Use available local and GitHub context, including:

* Historical PRs
* PR titles
* PR descriptions
* PR diffs
* Review comments
* Files changed
* Linked issues
* Commit history for changed files
* NuGet coding guidelines
* Nearby code conventions
* Existing tests

## Commit-Driven PR Discovery

**This is the primary discovery method.** Do not cherry-pick a handful of PRs. Instead:

1. Retrieve the last **500 commits** from the `dev` branch of `NuGet/NuGet.Client` using the GitHub API (paginate as needed, 100 per page).
2. For each commit, check if it was associated with a pull request (squash-merge commits reference the PR in the commit message, e.g., `(#1234)`).
3. Deduplicate — multiple commits may reference the same PR.
4. For each unique PR found:
   a. Fetch the PR's review comments / review threads.
   b. Skip PRs with zero review comments.
   c. For PRs with review comments, proceed to the Builder Process below.
5. Process PRs **one by one** — do not skip any PR that has review comments.
6. Track progress: log which PRs were analyzed, which had useful comments, and which rules were extracted.

This ensures comprehensive coverage of the team's actual review patterns, not just a hand-picked sample.

## Builder Process

For each discovered PR with review comments:

1. Read the PR title and description.
2. Inspect the changed files and diff (at minimum read the file list; read the diff for PRs with substantive review comments).
3. Read human review comments.
4. Ignore comments that are only:

   * praise
   * approval
   * nitpicks with no reusable value
   * outdated discussion
   * personal preference
   * resolved confusion with no general lesson
   * bot-to-bot exchanges (e.g., `@copilot fix this` → `Fixed in commit abc`)
5. For each useful comment, infer the reusable rule.
6. Check whether the inferred rule is already covered by an existing rule in `pr-review.md`. If so, strengthen the existing rule's evidence rather than creating a duplicate.

For each inferred rule, determine:

* What code pattern triggers this concern?
* Is this NuGet-specific, .NET-specific, or general C#?
* Is it repo-wide or component-specific?
* What files or areas does it apply to?
* Why does it matter?
* What should a reviewer check?
* What tests would usually be expected?
* When should the reviewer avoid commenting?
* Could this rule create noisy false positives?

## Rule Quality Bar

Only include a rule if it is:

* reusable
* specific enough to act on
* likely still valid
* useful for future reviews
* not already covered by a stronger existing rule

Prefer rules that appear repeatedly across PRs.

One-off rules may be included only if they protect correctness, compatibility, security, performance, AOT/trimming, restore behavior, protocol behavior, or public API stability.

## Final `pr-review.md` Structure

Generate a single final skill file using this structure:

```md
# NuGet PR Review Skill

## Purpose
Explain that this skill reviews NuGet PRs using historical repo knowledge, coding guidelines, and changed-file context.

## Review Principles
- Be precise and actionable.
- Avoid noisy comments.
- Prefer questions when intent is unclear.
- Distinguish blocking issues from suggestions.
- Treat historical PRs as context, not law.
- Prefer current code conventions over old comments.
- Do not invent historical context.

## Review Workflow

### 1. Understand the PR
Instructions for reading title, description, linked issue, changed files, and tests.

### 2. Classify changed files
Separate:
- modified existing files
- new files
- tests
- docs
- generated files

### 3. For modified existing files
Use file history and past PRs touching the same files as review context.

### 4. For new files
Use NuGet coding guidelines and nearby component conventions.

### 5. Apply NuGet review rules
Include the distilled rules learned from historical PRs.

### 6. Produce review output
Use a structured format.

## NuGet Review Rules

For each rule, use this format:

### Rule: `<name>`

**Applies when**
Describe trigger pattern.

**Reviewer reasoning**
Explain why this matters.

**What to check**
- ...

**Good comment shape**
Example wording.

**Avoid commenting when**
- ...

**Evidence**
Mention historical PR/comment pattern if known. Do not over-cite.

## Review Output Format

Use:

### Summary
Brief PR summary.

### High-risk areas
Only list areas relevant to the actual PR.

### Findings
Group as:
- Blocking
- Should fix
- Consider
- Questions

### Suggested tests
Specific tests to add or update.

### Historical context used
Only include if actual history was found.

## Noise Reduction Rules

- Do not comment just because code is different.
- Do not repeat obvious compiler/linter feedback.
- Do not suggest broad rewrites unless the PR creates real risk.
- Do not apply NuGet-specific rules to unrelated files.
- Do not force every PR through every risk category.
- Do not block on style unless it conflicts with repo conventions or harms readability.
```

## Validation Step

After generating `pr-review.md`, validate it against several historical PR comments.

For each historical useful comment, ask:

```text
Would the generated pr-review.md have helped produce this kind of comment?
```

If no, update `pr-review.md`.

If yes, keep the rule.

Then ask:

```text
Would this rule create noisy comments on unrelated PRs?
```

If yes, narrow or remove the rule.

## Final Output Requirements

When finished:

1. Write the final skill to:

```text
.github/skills/pr-review.md
```

2. Do not keep this builder skill in the repo.
3. Do not create many separate rule files unless explicitly asked.
4. Keep the final skill concise enough for an agent to actually use.
5. Prefer fewer high-quality rules over many weak rules.
6. Include NuGet-specific knowledge only when it is supported by historical PRs, coding guidelines, or repo conventions.

````