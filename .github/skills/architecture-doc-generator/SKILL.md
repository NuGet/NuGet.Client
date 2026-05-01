---
name: architecture-doc-generator
description: Generates an ARCHITECTURE.md file for a repository following matklad's guidelines. Use this when asked to create, generate, or regenerate an architecture document for a codebase.
---

# Architecture Document Generator

Generate an `ARCHITECTURE.md` file for a repository following the conventions described in
[matklad's ARCHITECTURE.md blog post](https://matklad.github.io/2021/02/06/ARCHITECTURE.md.html)
and modeled after the
[rust-analyzer architecture doc](https://github.com/rust-analyzer/rust-analyzer/blob/d7c99931d05e3723d878bea5dc26766791fa4e69/docs/dev/architecture.md).

## Document Structure

The generated file MUST follow this structure, in order:

1. **Title and introduction** — A short paragraph explaining what the document is and who it is for.
2. **Bird's Eye View** — A high-level overview of the problem being solved and the products/artifacts the repository ships. Keep it to a few paragraphs.
3. **Repository Layout** — A `tree`-style overview of the top-level directory structure, plus any solution filters, workspaces, or other mechanisms for loading subsets of the code.
4. **Code Map** — The heart of the document. Describes each significant project/crate/package: what it does and any design rules it enforces. Organized by architectural layer (foundation → core → commands → hosts → UI). Do NOT include per-project "Depends on: ..." lines — the dependency relationships are captured in the Mermaid diagram instead.
5. **Dependency Layers** — A Mermaid `block-beta` diagram showing the strict layering of the codebase. Dependencies flow downward only. Use ```` ```mermaid ```` fenced code blocks so GitHub renders the diagram natively. See the Mermaid diagram guidelines below.
6. **Cross-Cutting Concerns** — Shared source files, multi-targeting, build system, extensibility models, testing strategy, and other concerns that span multiple projects.

## Writing Guidelines

### What to include
- Name important files, modules, and types so readers can use symbol search to find them.
- Describe coarse-grained modules and how they relate to each other.
- Call out **API boundaries** — the interfaces where one subsystem meets another.
- Call out **Design Rules** (see below).
- Mention the dependency direction between layers.
- **Project heading links**: Each project heading in the Code Map MUST link to its project file (e.g., `.csproj`, `Cargo.toml`, `package.json`) using a **relative path** from the repository root. Use forward slashes in paths regardless of the local OS. Format: `#### [\`ProjectName\`](src/path/to/Project.csproj)`. Do NOT use absolute GitHub URLs — relative paths work on any hosting platform and survive forks/mirrors. To get the relative paths, enumerate the actual project files from the local clone — do not guess paths.

### What to exclude
- Do NOT go into detail about *how* each module works internally. The code map is a map of a country, not an atlas of maps of its states.
- Do NOT mention implementation-level interface names (e.g., `ICancelableTask`, `IDisposable`) unless they are part of the public API surface. Focus on *what* a component does, not which .NET interfaces it implements.
- Do NOT mention features that are not ready for public consumption, even if they exist in the code. When in doubt, omit.
- Do NOT include time estimates or dates.

### Design Rules
Use the label **"Design Rule:"** (bold) for important constraints the codebase deliberately maintains. These are the successor to what matklad calls "Architecture Invariants" — we prefer the name "Design Rule" because it is clearer and more actionable.

Design Rules are most valuable when they describe something that is **absent** from the code — a dependency that deliberately does not exist, a concern that a layer intentionally ignores. They are hard to discover by reading code alone.

Format them as standalone bold-prefixed paragraphs within the relevant code map section:

```markdown
**Design Rule:** `NuGet.Commands` knows nothing about MSBuild, Visual Studio, or any specific CLI framework. It is the shared business logic boundary.
```

### Accuracy Requirements
- **Target frameworks**: Always read the actual build configuration files (e.g., `Directory.Build.props`, `common.project.props`, `Cargo.toml`, `tsconfig.json`) to determine real target frameworks. Never guess or assume — the explore agent's summaries may hallucinate these.
- **Dependency graph**: If a DGML code map or similar artifact is provided, parse it programmatically (e.g., with a Node.js script) to extract the real dependency relationships. Do not rely solely on agent summaries for dependency data. Do NOT emit per-project "Depends on:" lines — the Mermaid diagram is the single source of truth for layer dependencies.
- **Public vs. internal**: Carefully distinguish public API surfaces (shipped as packages for third-party consumption) from internal implementation details. Do not list internal components under "Public API" headings or diagram sections.
- **MSBuild / build system details**: When describing build tasks, attribute functionality to the correct system (e.g., "uses MSBuild's static graph APIs" not "evaluates the project graph up-front").

### Mermaid Diagram Guidelines

Use Mermaid `block-beta` diagrams for the Dependency Layers section. These render natively on GitHub when placed inside ```` ```mermaid ```` fenced code blocks. Do NOT use ASCII art.

**Layout rules:**
- Use `columns 3` (or as appropriate) at the top level to allow side-by-side blocks for parallel host layers (e.g., Public API | CLI Hosts | MSBuild Hosts).
- Full-width layers (e.g., Command Layer, Protocol Layer) span all columns with `:3` syntax: `block:commands:3`.
- Each block contains a header node (styled transparent) and content nodes listing the projects in that layer.
- Use `\n` for line breaks within node labels to list multiple projects per node.
- Use `·` (middle dot) to separate items on the same line when space is tight.

**Arrows and flow:**
- Add downward arrows (`-->`) between layer blocks to show the dependency flow direction.
- Only show layer-to-layer edges, not individual project-to-project edges — the diagram is about coarse-grained layers.

**Styling:**
- Style header nodes with `fill:transparent,stroke:none` so they appear as plain text labels rather than boxes.
- Use emoji annotations sparingly for important callouts (e.g., `⚠️` for deprecated components, `⤷` for sub-components).

**Example skeleton:**
```
block-beta
  columns 3

  block:top_layer:3
    columns 1
    top_header["LAYER NAME"]
    top1["Project.A · Project.B"]
  end

  block:left_col
    lc_header["LEFT"]
    lc1["Project.C"]
  end
  block:middle_col
    mc_header["MIDDLE"]
    mc1["Project.D"]
  end
  block:right_col
    rc_header["RIGHT"]
    rc1["Project.E"]
  end

  block:bottom_layer:3
    columns 1
    bl_header["BOTTOM LAYER"]
    bl1["Project.F · Project.G"]
  end

  top_layer --> left_col
  top_layer --> middle_col
  top_layer --> right_col
  left_col --> bottom_layer
  middle_col --> bottom_layer
  right_col --> bottom_layer

  style top_header fill:transparent,stroke:none
  style lc_header fill:transparent,stroke:none
  style mc_header fill:transparent,stroke:none
  style rc_header fill:transparent,stroke:none
  style bl_header fill:transparent,stroke:none
```

## Process

When generating the architecture document:

1. **Request a DGML code map** — Before doing any work, ask the user to provide a DGML file for the repository. This file is essential for extracting accurate dependency relationships between projects. The user must generate it in Visual Studio by following the instructions at: https://learn.microsoft.com/en-us/visualstudio/modeling/analyze-and-model-your-architecture?view=visualstudio#dependency-diagrams. Do NOT proceed without this file.
2. **Explore the repository** — Read the top-level directory structure, solution/workspace files, build configuration, and any existing documentation (README, docs/ folder).
3. **Identify the products** — Determine the distinct artifacts the repo ships (executables, libraries, extensions, packages). Ask the user to describe the products if it is not obvious.
4. **Map the projects** — For each project/crate/package, read its build file to understand target frameworks, dependencies, and purpose. Read key source files to understand entry points and major types.
5. **Extract dependencies** — Parse the DGML file programmatically (e.g., with a Node.js script) to extract the real internal dependency graph. Do not rely on agent summaries for dependency data.
6. **Verify claims** — Cross-check target frameworks, dependency directions, and public/internal classifications against the actual source. Do not trust agent summaries blindly.
7. **Write the document** — Follow the structure and guidelines above. Keep it concise — every recurring contributor will have to read it.
8. **Review with the user** — The document will likely need domain-specific corrections. Be prepared to iterate.

## Reference

The current `ARCHITECTURE.md` for NuGet.Client at `Q:\src\Nuget.Client\ARCHITECTURE.md` is an example of the output this skill produces. Use it as a reference for tone, structure, and level of detail.

## Domain-Specific Patterns to Document

When generating architecture docs, watch for these patterns and document them with Design Rules:

### Deprecation / migration of subsystems
If a component is being replaced by a newer implementation, clearly mark the old one as **legacy/deprecated** and point to the replacement. Both the old component's section and the new component's section should cross-reference each other, and any summary diagrams should reflect the deprecation. Example: a legacy dependency resolver in one library being superseded by a new resolver class in a higher-level command library.

### Public SDK / extensibility API boundaries
If certain assemblies form a **public SDK** consumed by third-party developers, call this out explicitly. Link to the official documentation for the SDK. Add a Design Rule noting that any changes to these APIs constitute **breaking changes** and must follow the team's breaking-change process. These assemblies deserve extra scrutiny during code review.

### VS package early-load patterns
If a VS package auto-loads early in the IDE lifecycle, explain **why** it can do so cheaply. For example, a restore-manager package may contain a slimmed-down subset of assemblies specifically so it can load early without paying a performance cost. Document this design intent explicitly.

### MEF / service composition
When documenting MEF usage in VS extensions, list the key exports (services, factories, providers) but do NOT reference internal VS service infrastructure (e.g., `SComponentModel`, `SVsServiceProvider`) — those are implementation details that add noise without helping readers understand the architecture.
