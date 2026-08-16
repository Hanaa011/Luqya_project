# PHASE-2A – Part 1
## Enterprise AI Foundation

> **Document Type:** Software Design Specification (SDS)  
> **Audience:** Claude Code  
> **Status:** Phase 2A / Part 1

---

# 1. Purpose

This document defines the engineering rules and architectural foundation for transforming the current Lost & Found AI subsystem into an enterprise-grade, offline-first semantic intelligence platform.

**This phase does NOT implement the search engine.**

It establishes the foundation upon which all later phases will build.

---

# 2. Primary Objectives

- Replace provider-centric architecture with capability-centric architecture.
- Make external AI providers optional enhancements.
- Build a local-first AI foundation.
- Preserve backward compatibility where practical.
- Prepare the codebase for multilingual semantic intelligence.

---

# 3. Engineering Principles

The implementation must follow:

- Clean Architecture
- SOLID
- Dependency Injection
- Async-first APIs
- Thread Safety
- High Cohesion
- Low Coupling
- Testability
- Extensibility

Avoid:

- God classes
- Static mutable state
- Hidden dependencies
- Business logic inside provider classes
- Duplicate logic

---

# 4. Current Files in Scope

## Providers

- ClassificationJsonParser.cs
- ClassificationPromptBuilder.cs
- DeepSeekClassificationProvider.cs
- DeepSeekVisionHelper.cs
- GeminiClassificationProvider.cs
- GeminiEmbeddingProvider.cs
- GeminiVisionHelper.cs
- HuggingFaceClassificationProvider.cs
- HuggingFaceEmbeddingProvider.cs
- OllamaClassificationProvider.cs
- OllamaEmbeddingProvider.cs
- OllamaVisionHelper.cs
- OpenAIClassificationProvider.cs
- OpenAIEmbeddingProvider.cs

## Core

- AiMatchingService.cs
- AIProviderOptions.cs
- AiSearchAppService.cs
- ConfidenceCalibrator.cs
- LostFoundAiProvidersServiceCollectionExtensions.cs
- MatchExplanationGenerator.cs
- ObjectTypeRelationship.cs
- QueryProcessingCache.cs
- SearchTextProcessor.cs

---

# 5. Refactoring Rules

- Large classes become orchestration layers.
- Business logic moves into dedicated services.
- Providers become adapters.
- Public APIs remain compatible where possible.

---

# 6. Capability-Based Design

Instead of provider-oriented services:

- GeminiEmbeddingProvider
- OpenAIEmbeddingProvider

Design around capabilities:

- IEmbeddingEngine
- IEmbeddingRuntime
- IEmbeddingCache
- IEmbeddingStore
- IEmbeddingModelManager
- IClassificationEngine

Providers only implement these interfaces.

---

# 7. Suggested Folder Structure

```text
AI/
├── Core/
├── Embeddings/
├── Knowledge/
├── Concepts/
├── Graph/
├── Runtime/
├── Models/
├── Providers/
├── Importers/
├── Builders/
├── Caching/
├── Storage/
├── Languages/
├── Diagnostics/
└── Configuration/
```

---

# 8. Non-Functional Requirements

- Offline-first
- CPU optimized
- Future GPU support
- Low allocations
- Memory efficient
- Modular
- Production ready

---

# 9. Deliverables

Claude Code should produce:

1. Refactored architecture.
2. Provider abstraction.
3. New interfaces.
4. Dependency injection redesign.
5. Folder restructuring.
6. Foundation services.
7. Technical report describing every design decision.

---

# 10. Exit Criteria

This phase is complete only when:

- Architecture is capability-based.
- Providers are decoupled.
- Foundation services exist.
- Future embedding runtime can be added without changing business logic.
- Code is production-ready and documented.

> Phase 2A Part 2 will introduce the Local AI Runtime, ONNX integration, embedding models, model management, and offline execution.
