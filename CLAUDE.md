# CLAUDE.md — Enterprise Engineering Rules

> This repository contains an ABP Framework (.NET 10) Modular Monolith.
>
> Your responsibility is to extend the existing architecture while preserving
> stability, maintainability, and production quality.

---

# Before Starting Any Task

Always read:

1. docs/PROJECT-OVERVIEW.md
2. docs/PROJECT-GOALS.md

If the task is related to the Semantic AI engine, also read:

1. docs/Semantic-AI/README.md
2. docs/Semantic-AI/INDEX.md
3. The assigned Phase
4. The assigned Part

Never skip phases.

Never implement a future phase.

---

# Primary Mission

Build a production-grade multilingual Semantic AI Engine for the Lost & Found platform.

The system must:

- Work completely offline.
- Prioritize local intelligence.
- Continue operating when every external AI provider is unavailable.
- Integrate naturally with the existing ABP architecture.
- Preserve backward compatibility whenever possible.

External AI providers are enhancement layers only.

---

# Workspace Scope

Repository Root

C:\Users\Windows 11\Desktop\Forge

Primary implementation target

C:\Users\Windows 11\Desktop\Forge\modules\lostfound\src

Before writing code:

- Read the overall solution.
- Understand the ABP architecture.
- Understand module dependencies.
- Understand how LostFound integrates with the solution.

You are encouraged to inspect the entire solution for architectural understanding.

---

# Modification Scope

The LostFound module is the ONLY implementation target.

You may freely modify anything inside:

modules/lostfound/src/

Including:

- AI
- Providers
- BackgroundJobs
- Application Services
- Dependency Injection
- Configuration
- Documentation
- Internal architecture

The module may be internally redesigned if it improves architecture.

---

# Solution Awareness

You may inspect any project inside the Forge solution.

You may trace:

- Dependencies
- Service registrations
- Module interactions
- Shared infrastructure

However:

Implementation changes must remain inside the LostFound module unless a minimal integration change is strictly required.

---

# Allowed Solution-Level Changes

Minimal solution-level changes are allowed only when required for LostFound integration.

Examples:

- DI registrations
- Module registration
- appsettings
- NuGet packages
- Project references
- Build configuration

These changes must directly support the LostFound module.

---

# Forbidden Changes

Do NOT:

- Modify unrelated modules.
- Redesign the Forge solution.
- Rename projects.
- Move projects.
- Create new top-level folders.
- Reorganize the repository.
- Modify unrelated business logic.
- Introduce parallel architectures.

Leave unrelated modules untouched.

---

# Project Structure

Use the existing folder structure.

Do not reorganize it.

Do not rename folders.

Do not move existing files unless explicitly instructed.

New files must be placed inside existing modules.

Preferred locations include:

- AI/
- AI/Providers/
- BackgroundJobs/
- Reports/
- docs/
- docs/Semantic-AI/

Create new subfolders only when they logically belong inside an existing module.

Never create folders directly under the repository root.

---

# Existing Code Analysis

Before modifying any implementation:

- Read the existing code completely.
- Understand its purpose.
- Understand its dependencies.
- Preserve existing behavior unless redesign is required by the current phase.

Never rewrite code before understanding why it exists.

Prefer improving existing implementations over replacing them unnecessarily.

If redesigning a component, explain why the new design is superior.

---

# Safe Refactoring

Never remove code simply because it appears unused.

Before deleting anything:

- Verify it is obsolete.
- Verify nothing depends on it.
- Prefer deprecation over deletion.

Maintain backward compatibility whenever practical.

---

# Architecture Rules

Always follow:

- Clean Architecture
- SOLID
- Dependency Injection
- Async-first
- Thread Safety
- High Cohesion
- Low Coupling
- Production-ready quality

Never sacrifice architecture for short-term convenience.

---

# ABP Framework

This project uses:

- ABP Framework
- .NET 10
- Modular Monolith

Respect ABP conventions.

Reuse:

- Dependency Injection
- Application Services
- Background Jobs
- Configuration
- Module system
- Existing infrastructure

Avoid bypassing ABP patterns.

---

# Coding Standards

Write production-quality code.

Avoid:

- Code duplication
- Giant classes
- Giant methods
- Magic values
- Unnecessary allocations

Prefer:

- Composition
- Interfaces
- Dependency Injection
- Async APIs
- Readable code
- Maintainable architecture

---

# Dependency Policy

Before introducing a dependency:

- Prefer stable libraries.
- Prefer actively maintained projects.
- Prefer MIT or Apache-2.0 licenses.
- Avoid abandoned packages.
- Avoid unnecessary dependencies.
- Reuse existing dependencies whenever possible.

Every dependency must have a clear architectural justification.

---

# Local AI Policy

The primary intelligence layer must always be local.

Whenever possible:

- Local embeddings
- Local semantic search
- Local knowledge graph
- Local inference
- Offline datasets
- Cached semantic knowledge

External AI providers should only enhance results.

The application must continue functioning if every external AI provider becomes unavailable.

---

# Documentation

Major architectural decisions must be documented.

Whenever implementation changes the architecture:

Update the corresponding documentation inside:

docs/Semantic-AI/

Documentation must never become outdated.

---

# Build & Validation

Validate every major implementation.

You may execute:

- dotnet restore
- dotnet build
- dotnet test
- dotnet run

Run the complete Forge solution whenever necessary.

If build errors occur:

- Diagnose the root cause.
- Fix the issue.
- Rebuild.
- Repeat until successful.

Do not stop after the first error.

---

# Build Safety

Never consider a task complete until:

- The solution builds successfully.
- The application starts successfully.
- Dependency Injection succeeds.
- The LostFound module loads correctly.
- Existing functionality remains operational.
- No new compilation errors were introduced.

Always verify your implementation by rebuilding the solution.

---

# Phase Execution

Implement only the assigned phase.

Do not implement future phases.

Do not mix multiple phases.

Follow the documentation exactly.

If a requirement is unclear:

Stop and ask for clarification rather than making assumptions.

---

# Quality Goal

Your objective is not only to write code.

Your objective is to deliver a production-ready LostFound module that:

- Integrates correctly with the Forge solution.
- Preserves existing functionality.
- Improves semantic search quality.
- Supports offline AI.
- Is maintainable.
- Is extensible.
- Is suitable for enterprise production.

---

# Autonomous Execution Workflow

The execution order is defined in:

docs/Semantic-AI/INDEX.md

Follow it exactly.

For every Part:

1. Read the current Part completely.
2. Review the existing implementation.
3. Implement the current Part.
4. Integrate it into the existing ABP architecture.
5. Build the complete Forge solution.
6. Resolve all compilation errors.
7. Start the application when required.
8. Verify the LostFound module works correctly.
9. Verify existing functionality has not been broken.
10. Repeat build and validation until successful.

After completing the current Part:

- Generate a detailed Markdown report.
- Save it to:

C:\Users\Windows 11\Desktop\Forge\SemanticReports

Example:

Phase-1-Part-1-Report.md

The report should include:

- Objectives completed
- Files modified
- Files created
- Design decisions
- Build status
- Runtime validation
- Remaining issues
- Next Part

After saving the report:

Automatically continue to the next Part defined in:

docs/Semantic-AI/INDEX.md

Do not wait for user confirmation between Parts.

Continue until the entire roadmap has been completed.

Only stop if:

- A human decision is required.
- A blocking external dependency exists.
- The implementation cannot safely continue.

# Functional Validation

After the application starts successfully:

- Exercise the modified functionality.
- Verify that the new feature behaves as expected.
- Check application logs for runtime exceptions.
- Fix any discovered issues.
- Repeat validation until the feature works correctly.

A successful build alone does not mean the implementation is complete.

Every implemented feature must also be functionally validated whenever the local environment allows it.