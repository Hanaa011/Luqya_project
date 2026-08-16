# PHASE VALIDATION 06 — Local-First AI Classification Fallback & Ranking Calibration

## Background

Previous validation phases successfully stabilized the Semantic AI platform.

The current system already provides:

- Local embedding generation (BAAI/bge-m3)
- Hybrid semantic retrieval
- Hybrid ranking
- Background AI matching
- Multiple AI providers
- Retry policy
- Resilient provider decorator

However, production testing exposed a major architectural weakness.

---

## Current Production Behavior

During report creation:

```
Report Created
        │
        ▼
Classification Engine
        │
        ▼
OpenAI Provider
        │
        ├── Success
        │      ▼
        │  Classification Stored
        │
        └── Failure (429 / timeout / network / no subscription)
               ▼
        Retry (3x)
               ▼
        Classification Aborted
               ▼
        Category = null
        Object   = null
        Color    = null
        Brand    = null
               ▼
        Local Embedding
               ▼
        Matching Continues
```

Real production logs confirm this behavior.

The system never switches to another classifier.

Instead it simply skips AI classification.

**This is NOT the intended Local-First architecture.**

The Lost & Found module must remain fully operational in offline or isolated environments where no external AI provider is reachable.

---

## Required Architecture

The system must become fully Local-First.

External providers are optional enhancements.

They must never become mandatory.

The desired flow is:

```
Create Report
      │
      ▼
Classification Engine
      │
      ▼
Configured Provider
(OpenAI / Gemini / Ollama / etc.)
      │
      ├── Success
      │
      └── Failure
             ▼
      Automatic Local Classification
             ▼
      Category
      ObjectType
      Color
      Brand
      Tags
             ▼
      Store Classification
             ▼
      Generate Local Embedding
             ▼
      Hybrid Retrieval
             ▼
      Hybrid Ranking
             ▼
      Matching
```

At no point should classification become null merely because an external provider failed.

---

## Main Objective

Implement a complete Local AI Classification fallback.

If any remote provider fails due to:

- Timeout
- HTTP failure
- Authentication failure
- Quota exceeded
- Subscription missing
- Provider unavailable

the system must automatically classify locally.

- No user intervention.
- No configuration change.
- No failed report.

---

## Local Classification Requirements

The local classifier should extract at minimum:

- Category
- Object Type
- Color
- Brand
- Tags

using local resources only.

It may use:

- Ontology
- Concept graph
- Entity recognizer
- Vocabulary
- Semantic similarity
- Local embedding model
- Deterministic rules

or any combination that already exists in the project.

Do **NOT** introduce unnecessary external dependencies.

Prefer existing infrastructure.

The local classifier must not rely solely on simple keyword matching.

It should leverage the existing Semantic AI infrastructure wherever possible, including embeddings, ontology, entity recognition, concept graph, vocabulary expansion, and semantic reasoning.

Deterministic rules may be used only as complementary signals, not as the primary classification mechanism.

---

## Local AI Model Implementation

The project already uses ONNX Runtime for local embedding inference.

Prefer reusing the existing ONNX Runtime infrastructure instead of introducing another inference engine, unless there is a strong architectural reason not to.

If appropriate, implement the Local Classification Provider using a lightweight multilingual ONNX classification model that can run entirely offline.

The Local Classification Provider should integrate with the existing Classification Engine and remain transparent to callers.

The AI model should provide the initial semantic classification, while the existing Semantic AI infrastructure should refine, validate, and enrich the output using:

- Ontology
- Concept Graph
- Entity Recognizer
- Vocabulary Expansion
- Semantic Similarity
- Knowledge Graph (if available)

These components should improve the model output rather than replace it.

Avoid implementing the classifier primarily as keyword matching or deterministic rules.

The fallback mechanism must be provider-agnostic.

Any configured AI provider (OpenAI, Gemini, Ollama, or future providers) should automatically fall back to the Local Classification Provider through the existing Classification Engine.

The fallback must be implemented once within the classification layer and should not require provider-specific fallback logic.

---

## Ranking Improvement

While validating the fallback, improve Hybrid Ranking.

Current ranking still returns unrelated objects such as:

```
Laptop
  ↓
Car Keys
  ↓
Phone
```

These results should receive much stronger penalties.

Ranking should incorporate metadata.

Suggested signals include:

- Object Type
- Category
- Brand
- Color
- Location
- Temporal proximity
- Ontology relationship
- Semantic similarity
- Exact entity matches

Object Type mismatch should significantly reduce ranking.

Completely unrelated object classes should almost never appear among top candidates.

---

## Logging

Improve diagnostics.

Current logs only report:

```
Score = 82%
```

Instead produce explainable ranking logs.

Example:

```
Semantic Similarity : 0.82
Object Match         : +15
Category Match       : +10
Brand Match          : +5
Color Match          : +10
Ontology Bonus       : +8
Penalty              : -40
Final Score          : 82.7
```

The logs should clearly explain why each result received its final score.

---

## Validation

Do **NOT** assume the implementation works.

Perform real validation.

Use production execution.

Verify all scenarios.

### Required Tests

**Test 1 — OpenAI unavailable**

Expected:
- Local classification executes automatically.
- Category is populated.
- ObjectType is populated.
- Brand is populated.
- Color is populated.
- Matching still succeeds.

**Test 2 — Gemini unavailable**

Expected:
- Same behavior as Test 1.

**Test 3 — No internet connection**

Expected:
- Entire AI pipeline continues locally.

**Test 4 — Multiple Arabic descriptions**

Expected:
- Verify local classifier accuracy.

**Test 5 — Multiple English descriptions**

Expected:
- Verify local classifier accuracy.

**Test 6 — Mixed Arabic + English**

Expected:
- Verify multilingual classification.

**Test 7 — Ranking quality**

Expected:
- Verify that unrelated objects receive significantly lower scores than semantically similar objects.

---

## Non-Goals

This validation is NOT intended to:

- Remove support for OpenAI, Gemini, Ollama, or any existing provider.
- Disable cloud providers.
- Replace the current provider architecture.
- Rewrite the AI subsystem from scratch.
- Introduce a new parallel classification pipeline.

The objective is to improve the existing architecture by adding an automatic Local-First fallback while preserving all existing providers and behaviors.

---

## Acceptance Criteria

The implementation is considered successful only if all of the following are true:

- Creating a report succeeds even when every cloud provider is unavailable.
- Classification fields are still populated using local AI.
- No null classification is produced solely because a cloud provider failed.
- Hybrid retrieval continues to work.
- Hybrid ranking quality is maintained or improved.
- Existing cloud providers still function normally when available.
- No regression is introduced into existing matching functionality.

---

## Constraints

- Read the entire Lost & Found module before modifying anything.
- Understand the complete Semantic AI architecture.
- Do not duplicate pipelines.
- Do not replace existing architecture.
- Integrate with the current design.
- Maintain backward compatibility.
- Preserve all existing functionality.
- Avoid unnecessary abstractions.
- Use existing services whenever possible.

---

## Deliverables

At completion provide:

1. Root cause analysis.
2. Architectural decisions.
3. Files modified.
4. Detailed implementation explanation.
5. Validation evidence.
6. Ranking improvements.
7. Before vs After comparison.
8. Real production logs proving that:
   - External provider failure no longer causes null classification.
   - Local classification automatically replaces the failed provider.
   - Hybrid ranking quality improved.
   - The complete pipeline operates successfully without any cloud AI provider.