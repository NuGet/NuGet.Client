---
name: pr-review
description: >-
  review a nuget pr
---
# NuGet PR Review Skill

## Purpose

Review NuGet.Client PRs using historical repo knowledge and NuGet engineering guidance. Produce high-signal comments about correctness, compatibility, restore behavior, public API risk, localization, and maintainability — not style noise.

## Review Principles

- Be precise and actionable. Say what is risky, why, and what to verify.
- Prefer questions when intent is unclear.
- Distinguish blocking issues from suggestions.
- Treat historical PRs as guidance, not immutable law.
- Only apply a rule when the changed files actually trigger it.
- Prefer one strong comment over several overlapping weak ones.

## Review Workflow

1. **Understand the PR** — Read title, description, linked issue, and customer scenario. Note whether it touches restore, CLI UX, public APIs, warnings, serialization, localization, packaging, or VS integration.
2. **Classify changed files** — Separate: modified product files (preserve behavior), new product files (minimal surface), tests (coverage quality), build/config/packaging (repo-wide impact), docs/strings/generated (correctness).
3. **For modified files** — Use file history to understand the original contract. Watch for silent breaks in validation, serialization, warnings, output, and restore. Check net472, VS, CLI, and AOT scenarios.
4. **For new files** — Prefer narrowest visibility. Follow nearby conventions. If customer-facing, check terminology, localization, and test coverage.
5. **Apply rules** — Use only the rules relevant to the PR's risks.
6. **Produce output** — Use the Review Output Format below.

---

## NuGet Review Rules

Rules are grouped by theme. Each rule lists: when it applies, what to check, and example comment shape. Evidence cites historical PRs where the pattern was caught.

---

### Serialization & Format Compatibility

#### `json-migration-parity-and-safety`

**Applies when** a PR migrates JSON handling (NSJ→STJ), adds converters, reads `Utf8JsonReader` directly, stores `JsonElement`, or changes persisted/versioned formats.

**What to check**
- Do accepted token kinds and coercions match NSJ behavior? Are parity tests using the same payload against both?
- Is `reader.HasValueSequence` handled (or a helper like `CoerceScalarTokenToString` used) to avoid multi-segment truncation?
- Does `[JsonRequired]` cover both missing *and* null when the old NSJ contract rejected both?
- Is `JsonElement`/`JsonDocument` retained on cached/long-lived models (retains full backing document)?
- For persisted formats: are version strings and field names centralized in named constants? Do optional/missing fields deserialize to correct fallback? Is equality/hash stable across `null` vs `string.Empty`?

**Example** "The NSJ path accepts this payload differently. Can we add a parity test and also handle `HasValueSequence` here to avoid multi-segment truncation?"

**Evidence** PRs #7355–#7360, #7297, #7299, #7217, #7105, #7128, #7096, #6972, #6081.

---

### Public API & Compatibility

#### `public-api-surface-and-tracking`

**Applies when** a PR adds/broadens public types or members, changes public contracts, or modifies `PublicAPI.*.txt`.

**What to check**
- Is the API genuinely needed by external consumers, or can it stay `internal` until the shape stabilizes (rule of three)?
- Are XML docs added for new public members?
- Is `PublicAPI.Unshipped.txt` updated (not Shipped) for new APIs? Can per-TFM API files be unified?
- Does tightening validation or adding `required` on public types create a source/binary breaking change?

**Example** "Do we need this public yet? If only product code consumes it and the shape may move, `internal` avoids locking the contract."

**Evidence** PRs #7193, #7201, #7176, #7217, #6777, #6703; docs/nuget-sdk.md.

#### `assembly-boundaries-and-seams`

**Applies when** a PR shares internals between product assemblies, puts test-only helpers in product code, uses reflection, DTE, or static/global lookups.

**What to check**
- IVT is only for test assemblies, not product-to-product.
- Tests use internal seams, not reflection.
- Dependencies are passed explicitly or via DI; prefer VS service APIs over DTE.
- Test-only helpers don't leak into shipping assemblies.

**Example** "Can we expose an internal seam or use the VS service API instead of reflection/DTE here?"

**Evidence** PRs #7213, #7103, #7102, #6758, #7036, #7169, #7133; docs/coding-guidelines.md.

#### `cross-target-and-aot-compatibility`

**Applies when** a PR changes code shared across net472/modern .NET, uses `Assembly.Location`, or modifies packaging/bundling.

**What to check**
- Does behavior still work on net472 and older supported VS versions?
- Is `AppContext.BaseDirectory` used instead of `Assembly.Location` (empty in single-file/AOT)?
- After packaging changes, was the feature validated in older VS?

**Example** "This looks fine on modern .NET, but does it still behave on net472? And `Assembly.Location` can be empty in AOT — should we use `AppContext.BaseDirectory`?"

**Evidence** PRs #7297, #7197, #6697, #6787.

---

### Nullability & Type Safety

#### `truthful-nullability-and-modeling`

**Applies when** a PR changes nullable annotations, uses `!`, adds `TryGet`/`TryCreate` APIs, or models mutually exclusive states.

**What to check**
- Does `[NotNullWhen(true)]` hold on every success path?
- Is `!` truly justified, or can the contract be expressed directly?
- Can old `Debug.Assert(x != null)` patterns be removed when the type is already non-nullable?
- Were AI-generated nullability edits verified against the actual contract?
- New properties: prefer `init` over `set` when write-once; use `required` carefully on public types.
- Mutually exclusive fields: would separate types or records make invalid states unrepresentable?
- Equality/hash: can record-generated equality replace custom logic? Is null/empty/path normalization consistent?

**Example** "Can this contract be expressed directly instead of `!`? Also, would separate types make these mutually exclusive fields impossible to misuse?"

**Evidence** PRs #7178, #7217, #7239, #7299, #6678, #6850, #6805, #6972.

#### `validate-at-boundary-with-precision`

**Applies when** a PR validates package IDs, paths, TFMs, CLI inputs, or customer-provided strings, or introduces comparisons/constants.

**What to check**
- Is validation at the layer with the final customer-visible value?
- Prefer safe allowlists over brittle deny-lists.
- Report all input errors, not just the first.
- Package IDs: `OrdinalIgnoreCase`. Paths: normalize separators before sort/compare.
- Use existing typed constants (e.g., `LibraryType`) instead of magic strings.

**Example** "Can we validate at the boundary with the final value, use an allowlist, and `OrdinalIgnoreCase` here?"

**Evidence** PRs #7244, #7213, #6567, #6678, #7017, #6963.

---

### Testing & Coverage

#### `tests-cover-real-paths-and-distinguish-branches`

**Applies when** a PR adds/updates tests for commands, restore, configuration, or integration flows.

**What to check**
- Does the test hit the real command/config path the feature reads, not just a helper?
- Do assertions distinguish similar branches (specific IDs/versions/TFMs, not just success/failure)?
- If tests or TFMs were removed, is equivalent coverage proven elsewhere?
- If a feature mirrors an existing one, are distinct scenarios tested?
- For UI changes: are screenshots provided? Were resolution and PLOC scenarios considered?

**Example** "Can the assertion prove *which* branch was taken? A more specific check would distinguish mapped vs unmapped here."

**Evidence** PRs #7096, #6475, #6973, #6953, #7345, #6044, #6110.

#### `warning-and-suppression-coverage`

**Applies when** a PR adds or changes restore warnings.

**What to check**
- Is `NoWarn` suppression coverage added (package-level and project-level)?
- Could existing dedup sets accidentally suppress the new warning?
- For TFM-specific warnings: is `TargetGraphs` populated and asserted?

**Example** "Please add suppression coverage and check whether this shares a dedup bucket with existing warnings."

**Evidence** PRs #7229, #5731, #5925, #6374.

#### `test-quality-and-isolation`

**Applies when** a PR adds tests with opaque data, mutable fixtures, external tool shelling, or network-looking inputs.

**What to check**
- Would named `[Fact]` tests be clearer than opaque `MemberData`?
- Are mutable inputs deep-cloned? File handles disposed before child processes?
- Use `.test` TLD for test URLs. Prefer inline csproj edits over `dotnet add package`.
- Do test names accurately describe condition and expectation?
- Are stale `[Skip]`/`[Ignore]` annotations linked to now-closed issues? Remove debug leftovers.

**Example** "Would separate named facts be easier to diagnose? Also, this fixture is mutated across cases — can we deep-clone?"

**Evidence** PRs #6953, #6948, #7155, #7374, #7246, #6345.

#### `behavioral-coverage-when-replacing-code`

**Applies when** a PR rewrites an implementation or replaces a component.

**What to check**
- Were old behavioral tests ported or intentionally retired with justification?
- Did the replacement flatten structure that encoded important product rules?
- If fixing a shared utility, were all callers audited for the same bug?
- If a cache/sync helper must track every property, is there a guard test for drift?

**Example** "Since this replaces the old implementation, can we port the prior behavioral tests so we know semantics stayed intact?"

**Evidence** PRs #6826, #6973, #7148, #7193, #5980.

---

### Performance & Resources

#### `hot-path-allocation-caching-and-disposal`

**Applies when** a PR adds work in restore, per-package, per-request, or repeatedly-called paths, or introduces disposable resources.

**What to check**
- Are collections materialized only to `foreach` them? Use `IEnumerable`/`Enumerable.Empty` over `new List`/`ToArray`.
- Are env vars, SourceRepository instances, or initialization results recomputed unnecessarily?
- For simple char checks: prefer span loops over regex. Prefer built-in APIs over conditional compilation/PInvoke.
- Owned disposable fields/streams: disposed explicitly, before external processes need the file.
- Dictionary capacity: consider prime-number sizing; consider `ConcurrentDictionary` under contention.

**Example** "This path runs per-package. Can we avoid the allocation and reuse the existing result? Also, this stream should be disposed before the child process runs."

**Evidence** PRs #7297, #7298, #6768, #6726, #6805, #7088, #7374, #7213, #6787, #5862.

---

### Build, Restore & Packaging

#### `sdkanalysislevel-gating-and-rollout`

**Applies when** a PR adds a new warning, promotes warning→error, or changes a restore/build default.

**What to check**
- Is existing behavior preserved below the intended `SdkAnalysisLevel`?
- Is `SdkAnalysisLevel` used only to choose defaults, not as the feature switch itself?
- Is rollout staged from warning→error across SDK versions?

**Example** "This changes restore output. Should it be gated by `SdkAnalysisLevel` with a warn-first/error-later rollout?"

**Evidence** PRs #7229, #7244, #5833, #5925; docs/feature-guide.md.

#### `restore-resolution-and-source-mapping`

**Applies when** a PR changes restore/package resolution, source mapping, or assets file processing.

**What to check**
- With PackageSourceMapping, are only mapped sources considered? Fail early on unmapped.
- Are unresolved packages surfaced as failures, not emitted as resolved?
- When reading assets/project references, is filtering by item type needed?
- Does the change respect all modes (e.g., `NuGetAuditMode` direct vs all)?

**Example** "If no mapped source satisfies this package, should we fail early rather than emitting it as resolved?"

**Evidence** PRs #6953, #6903, #6678, #6237.

#### `packaging-dependencies-and-config-hygiene`

**Applies when** a PR changes build/pack outputs, `NuGet.Config` feeds, dependency versions, MSBuild/YAML conditions, or project structure.

**What to check**
- Generated/temp build artifacts not accidentally included in PR.
- After pack/build changes, was `.nupkg`/VSIX content verified?
- Stale project/solution entries removed after file churn.
- `NuGet.Config` uses approved public feeds (e.g., azure-public), not private/internal.
- Integrated tool versions pinned and tested. Redistributed DLLs need `NOTICES.txt`/`.vsixignore`.
- Risky dependency upgrades isolated from unrelated changes. References scoped to actual consumers.
- Shared MSBuild conditions at highest level; don't shadow SDK property names.
- Same identifier in code/config/docs/tests → automated consistency check or centralization.

**Example** "Can we keep this dependency upgrade isolated, verify the nupkg content, and make sure the feed reference uses the approved public source?"

**Evidence** PRs #6697, #6575, #6834, #6665, #7036, #6771, #6539, #6564, #7168, #7152, #5844.

---

### Localization & User-Facing Strings

#### `localization-workflow-and-phrasing`

**Applies when** a PR changes user-visible strings, `.resx`/`.Designer.cs`/`.xlf` files, or localized placeholders.

**What to check**
- User-facing strings added through `.resx`. `.Designer.cs` and `.xlf` are generated, not hand-edited.
- Localized placeholders exactly match source. Full phrases in one resource, not concatenated fragments.
- UI/link text doesn't assume English word order.
- Leave comments for localizers when square brackets, URLs, or technical terms must not be translated.

**Example** "Can this be one localized resource instead of concatenating fragments? That gives translators freedom to reorder."

**Evidence** PRs #6665, #6755, #6959, #6393; docs/localizability.md.

#### `customer-terminology-and-messages`

**Applies when** a PR introduces user-facing option names, telemetry/event names, error/warning messages, or product terminology.

**What to check**
- Names that ship (telemetry, CLI flags, env vars) are precise and consistent across title/docs/code/tests/output.
- Error/warning messages reflect the actual failure scenario and use NuGet's established terminology/error codes.
- Reuse existing error codes when behavior/message already match. Don't invent new ones needlessly.
- Exception catching/wrapping at the last boundary preserving customer context.
- Product naming follows guidance (e.g., "GitHub Copilot" not vague shorthand). Avoid "please" in error messages.
- Version display format consistent across commands.

**Example** "This shipped name will likely stick. Can we make it more precise and reuse the existing error code here?"

**Evidence** PRs #7213, #7201, #6560, #6557, #7017, #7036, #6850, #6805, #6768, #6563, #6555, #5702.

---

### Code Quality & Maintainability

#### `structure-callers-and-composition`

**Applies when** a PR refactors shared utilities, flattens structure, duplicates feature logic, or pushes output/logging decisions into shared code.

**What to check**
- Does current structure encode domain rules that would be lost if flattened?
- If fixing a shared helper, were all callers reviewed for the same bug?
- When features mirror each other, can shared helpers be extracted?
- Shared logic returns structured data; caller boundary decides how to log/render.
- Pass related data through existing model classes, not as separate loose parameters.
- Separate concerns: a class doing graph walk + formatting + printing should be split.

**Example** "This refactor simplifies the shape, but does the original structure encode a product rule? Also, could this layer return data and let the caller decide how to render?"

**Evidence** PRs #6826, #6970, #7148, #6778, #6777, #7169, #6565, #5761, #6237.

#### `systemic-fixes-and-debt-tracking`

**Applies when** a PR handles analyzers/debt with local suppressions, temporary workarounds, or leaves follow-up work undocumented.

**What to check**
- Fix the analyzer issue at the source rather than adding local suppressions.
- If debt remains, link a follow-up issue in code or PR discussion.
- Revert whitespace-only/formatting-only changes to unrelated files.
- Remove dead code, stale config, and debug leftovers before merge.

**Example** "Could we fix the underlying issue instead of suppressing? If we must leave debt, please link the follow-up issue."

**Evidence** PRs #6834, #6565, #7036, #6696, #6563.

#### `offline-and-downstream-compatibility`

**Applies when** a PR changes restore network behavior, CLI output formatting, or other behavior consumed by tooling/scripts.

**What to check**
- Was blocked/offline `nuget.org` behavior reviewed?
- If CLI output changed, were downstream parsers/consumers considered?
- Are rare failure paths still actionable for the customer?

**Example** "This output may be consumed by scripts or offline environments. Have we checked blocked-nuget.org and downstream consumers?"

**Evidence** PRs #6771, #6565, #6768.

---

## Review Output Format

### Summary
Brief PR summary focusing on where the risk is.

### High-risk areas
Only list relevant ones: serialization parity, restore behavior, public API, CLI UX, localization, packaging, test coverage.

### Findings
Group as:
- **Blocking** — correctness, security, compatibility, data loss, restore regression.
- **Should fix** — strong maintainability/test gaps likely to cause regressions.
- **Consider** — worthwhile improvements with moderate risk.
- **Questions** — intent unclear; ask before prescribing.

Each finding: file/line, concern, why it matters, suggested fix or question.

### Suggested tests
Only tests that materially reduce risk for this PR.

### Historical context used
Only when you actually relied on history. Cite specific PRs/rules.

---

## Noise Reduction Rules

- Don't comment just because code differs from your preference.
- Don't repeat compiler/analyzer/linter feedback unless the PR suppresses it incorrectly.
- Don't force every PR through every rule. Don't block on style unless it harms correctness or readability.
- Don't suggest broad rewrites when a targeted fix addresses the real risk.
- Don't apply NuGet restore/CLI rules to unrelated files.
- Don't demand more tests when existing coverage already proves the changed behavior.
- Don't comment on `.xlf` translation content; focus on workflow correctness.
- Don't leave multiple comments for one underlying issue — consolidate.
- For AI-authored changes, focus on behavioral risk, not stylistic cleanup.
