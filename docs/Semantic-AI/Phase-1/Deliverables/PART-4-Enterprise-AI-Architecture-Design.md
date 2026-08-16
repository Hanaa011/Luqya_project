# PHASE 1 — Part 4 (Deliverable)
# Enterprise AI Architecture Design

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-4-Enterprise-AI-Architecture-Design.md`.
> Builds on Part 2 (current-state findings) and Part 3 (technology selections). Architecture only — no production code was written.

---

## 1. High-Level Architecture

The target architecture keeps the existing three-service surface (`AiSearchAppService`, `ReportAppService`, `ReportMatchingBackgroundJob`) but replaces what today is a single God Class (`AiMatchingService`, Part 2 §2.1) and five static utility classes with a **pipeline of independently replaceable modules**, each behind an interface, composed via ABP's existing DI container. No new services are introduced at the infrastructure level (per Part 3 ADR-1) — this is a **modularization of the existing process**, not a distributed-systems rewrite.

```
                    ┌─────────────────────────────────────────┐
                    │   Business Layer (unchanged)             │
                    │   AiSearchAppService / ReportAppService  │
                    │   ReportMatchingBackgroundJob             │
                    └───────────────────┬───────────────────────┘
                                        │
                    ┌───────────────────▼───────────────────────┐
                    │   Semantic Services (NEW — Part 4)         │
                    │   IQueryPipeline  /  IClassificationPipeline│
                    └───────────────────┬───────────────────────┘
                                        │
      ┌──────────────┬──────────────┬──┴───────────┬───────────────┬───────────────┐
      ▼              ▼              ▼               ▼               ▼               ▼
 ILanguage      IKnowledge     IEmbedding      IHybrid          IRanking       IMatchExplanation
 Pipeline       Graph          Engine          SearchEngine     Engine         Service
 (detect,       (concepts,     (local ONNX +   (BM25+dense+    (feature-      (existing logic,
 normalize,     relations,     provider        RRF fusion +    based scoring, relocated behind
 spell-correct) taxonomy)      fallback)        candidate gen)  Part 2's       an interface)
                                                                 CandidateScore
                                                                 generalized)
      │              │              │               │               │               │
      └──────────────┴──────────────┴───────────────┴───────────────┴───────────────┘
                                        │
                    ┌───────────────────▼───────────────────────┐
                    │   Abstractions (interfaces — this Part's   │
                    │   §7 Interface Inventory)                  │
                    └───────────────────┬───────────────────────┘
                                        │
                    ┌───────────────────▼───────────────────────┐
                    │   Infrastructure                            │
                    │   ONNX Runtime host, in-process vector index,│
                    │   IDistributedCache-backed query cache,      │
                    │   dataset importer pipeline                  │
                    └───────────────────┬───────────────────────┘
                                        │
                    ┌───────────────────▼───────────────────────┐
                    │   AI Providers / Local Models                │
                    │   Gemini/OpenAI/Ollama/HuggingFace/DeepSeek  │
                    │   (existing, retained as fallback tier)      │
                    │   + local BGE-M3 ONNX model (Part 3)         │
                    └─────────────────────────────────────────────┘
```

Business logic (`AiSearchAppService`, `ReportAppService`) never changes its dependency surface — it still depends only on one top-level interface (`IAiMatchingService`, or its successor `IQueryPipeline`), satisfying Part 1 Principle 1 (Provider Independence) and preserving the "Backward Compatibility whenever possible" instruction in `CLAUDE.md`.

---

## 2. Component Design

For each new/relocated component: purpose, responsibilities, public interface, dependencies, lifecycle, thread safety, caching, extension points.

### 2.1 Query Processing (`IQueryPipeline`)
- **Purpose**: replaces `AiMatchingService.FindSimilarReportsAsync` as the top-level orchestrator.
- **Responsibilities**: sequence the pipeline stages (§5), nothing else — nearly all logic Part 2 found crammed into `AiMatchingService` moves to the modules below.
- **Dependencies**: `ILanguagePipeline`, `IConceptResolver`, `IEmbeddingEngine`, `IHybridSearchEngine`, `IRankingEngine`, `IConfidenceCalibrator`, `IMatchExplanationService`.
- **Lifecycle**: `ITransientDependency` (unchanged from today).
- **Thread safety**: stateless coordinator; safe by construction.
- **Extension point**: pipeline stage list itself could become configurable (ordered stage list) in a later phase, but Phase 2A keeps it a fixed, readable sequence — configurability here is explicitly **not** justified yet (avoid speculative abstraction).

### 2.2 Language Processing (`ILanguageDetector`, `ITextNormalizer`, `ISpellCorrector`)
- **Purpose**: replaces the ad-hoc Arabic-only logic in `SearchTextProcessor` (Part 2 §2.9) and `MatchExplanationGenerator.LooksArabic` (Part 2 §2.6) with real, multilingual, swappable services.
- **Responsibilities**: `ILanguageDetector` — script fast-path + Ar/Ur n-gram disambiguation (Part 3 §7). `ITextNormalizer` — per-language normalization (Arabic letter-forms today, Urdu equivalent added). `ISpellCorrector` — SymSpell-backed correction (Part 3 §7), one dictionary per supported language.
- **Dependencies**: `ITextNormalizer`/`ISpellCorrector` depend on `IKnowledgeGraph`-sourced dictionaries at initialization (dataset-derived, not hardcoded — closes Part 2 §2.9's "hardcoded, tiny, non-extensible" finding).
- **Lifecycle**: `ISingletonDependency` for the loaded dictionaries/models (expensive to build, safe to share — pure lookups after construction); `ITransientDependency` for any per-request state (none expected).
- **Thread safety**: singleton state must be read-only after warm-up; SymSpell's dictionary structure is immutable post-load, safe for concurrent reads.
- **Caching**: none needed beyond the loaded dictionary itself (no per-request cache).
- **Extension point**: adding a language means adding a dictionary/dataset + registering it, not editing code — directly satisfies Part 1 Principle 5.

### 2.3 Knowledge Graph (`IKnowledgeGraph`, `IConceptResolver`, `ISemanticExpander`)
- **Purpose**: replaces `ObjectTypeRelationship`'s hardcoded 5-cluster table (Part 2 §2.7) and `SearchTextProcessor.SynonymMap` (Part 2 §2.9) with the queryable concept graph designed in Part 5.
- **Responsibilities**: `IKnowledgeGraph` — low-level concept/relationship storage and lookup. `IConceptResolver` — word → Concept ID resolution (replaces the synonym map). `ISemanticExpander` — Concept → related Concepts expansion (replaces `ObjectTypeRelationship.Classify`'s cluster logic with a real `IS_A`/`RELATED_TO` graph traversal).
- **Dependencies**: backing store selected in Part 5 (SQLite/binary index — decided there, not here).
- **Lifecycle**: `ISingletonDependency` — the graph is loaded once at startup and queried read-only per request (mirrors §2.2's reasoning).
- **Caching**: in-memory concept lookup is itself the cache; no additional layer needed at this scale.
- **Extension point**: new relationship types, new concepts, new languages are **data**, imported via the Part 8 dataset pipeline — never a code change. This is the component that most directly fixes Part 2's "brittle, hand-maintained, prompt-vocabulary-coupled table" finding.

### 2.4 Embedding Engine (`IEmbeddingEngine`)
- **Purpose**: replaces the always-external `IEmbeddingProvider` calls in `AiMatchingService`/`ReportMatchingBackgroundJob` (Part 2 §2.1, §2.11) with a **local-first** engine.
- **Responsibilities**: generate text embeddings via the local ONNX BGE-M3 model (Part 3 §3/§8) as the primary path; fall back to the existing external `IEmbeddingProvider` chain only when local inference is unavailable/unconfigured (Part 1's "Local-First Strategy" ladder: Local Embeddings → Cached AI → External AI).
- **Dependencies**: ONNX Runtime session (infrastructure), existing `IEmbeddingProvider` implementations (retained, now a fallback rather than the only path).
- **Lifecycle**: `ISingletonDependency` for the loaded ONNX session (model load is expensive; ONNX Runtime sessions are thread-safe for concurrent inference by design).
- **Thread safety**: ONNX Runtime `InferenceSession.Run` is safe for concurrent calls from multiple threads against one session — no additional locking needed.
- **Caching**: retains `QueryProcessingCache`'s intent, now behind `IQueryProcessingCache` (§2.7) — same normalized-text/embedding caching, properly abstracted this time.
- **Extension point**: swapping the embedding model (e.g. multilingual-e5-base fallback, or a future model) is a configuration change, not a code change — directly satisfies Part 1 Principle 4 (Replaceability).
- **Note — true visual embeddings**: Part 2 §2.10 flagged that "image embedding" today is caption-then-text-embed everywhere. This component's interface (`GenerateImageEmbeddingAsync`) is designed to allow a **true joint image embedding model** to be substituted later without changing any caller — but selecting/adopting such a model is **out of scope for Phase 1/2A** (no CPU-friendly, ONNX-exportable, multilingual-safe candidate was evaluated in Part 3 — flagged as a Part 9/future-phase research item, not silently dropped).

### 2.5 Hybrid Retrieval (`IHybridSearchEngine`, `IVectorIndex`)
- **Purpose**: implements Part 3's ADR-3 (BM25 + dense fused via RRF) as the candidate-generation stage ahead of scoring — replaces the current full linear scan (Part 2 §6).
- **Responsibilities**: `IVectorIndex` — in-process ANN index (HNSW/FAISS per Part 3 §2) over candidate embeddings, with incremental add/update as reports are created (avoiding a full-rebuild-per-search). `IHybridSearchEngine` — runs BM25-style lexical search and `IVectorIndex` similarity search in parallel, fuses via RRF, returns a bounded shortlist (e.g. top 100–200) to the ranking stage.
- **Dependencies**: `IVectorIndex`, `IEmbeddingEngine`, `IReportRepository` (existing).
- **Lifecycle**: `IVectorIndex` is `ISingletonDependency` holding mutable index state — **this is the one component in the design that is not read-only after warm-up**, so it requires explicit thread-safety design (reader-writer lock or a lock-free structure, depending on the chosen HNSW library's own guarantees) — flagged explicitly as a concurrency risk to design carefully in Phase 2A, not deferred silently.
- **Extension point**: swapping BM25 for a different lexical scorer, or the ANN library itself, is isolated to this module.

### 2.6 Ranking Engine (`IRankingEngine`, `IScoreComponent`)
- **Purpose**: generalizes `AiMatchingService.BuildCandidateScore`/`ScoringWeights` (Part 2 §2.1) into a composable engine instead of a fixed method.
- **Responsibilities**: run a configured ordered list of `IScoreComponent` implementations (text similarity, image similarity, object-type match, color match, brand match, tag overlap, dynamic boosts, penalty tiers — every signal that exists today, unchanged in *behavior*, relocated in *structure*) and sum into a `CandidateScore`, exactly as today, but now each component is independently unit-testable and independently addable.
- **Dependencies**: `IKnowledgeGraph`/`ISemanticExpander` for the object-type-relationship signal (replacing `ObjectTypeRelationship`'s static table, per §2.3).
- **Extension point**: this is precisely where the still-zero `LocationBonus`/`DateBonus` stubs (Part 2 §2.1) become real `IScoreComponent` implementations later, without touching anything else — directly satisfies Part 1 Principle 5.

### 2.7 Confidence Engine (`IConfidenceCalibrator`)
- Same logic as today's `ConfidenceCalibrator` (Part 2 §2.4), relocated behind an interface with its control points sourced from configuration rather than hardcoded constants — the only substantive change. Piecewise-linear, monotonic-by-construction behavior is explicitly **preserved**, not redesigned (per `CLAUDE.md`: prefer improving over replacing working implementations).

### 2.8 Explanation Engine (`IMatchExplanationService`)
- Same deterministic, local, provider-independent logic as today's `MatchExplanationGenerator` (Part 2 §2.6), but restructured from per-language builder methods into **one template engine + per-language resource sets**, resolved via `ILanguageDetector` (§2.2) instead of the binary `LooksArabic` check — this is the one component where Part 2 explicitly recommended structural change, not just relocation, because the current duplication (`BuildEnglish`/`BuildArabic`) cannot extend to Urdu/Hindi/Turkish/Persian/Malay/French without repeating itself N more times.

### 2.9 AI Provider Adapters (existing `AI/Providers/*`, retained)
- **Responsibilities**: unchanged externally; internally, Part 2 §2.10/§8's resiliency gap is closed by introducing a shared `ResilientProviderDecorator` (retry/backoff/timeout/circuit-breaker) wrapping every provider uniformly, rather than only Gemini having it.
- **Extension point**: the DI registration switch (Part 2 §2.5) is replaced by a small provider registry so a new provider (notably a future local-inference provider) self-registers rather than requiring an edit to the composition-root switch statement (closes the OCP violation Part 2 flagged).

### 2.10 Dataset Import Pipeline (`IDatasetImporter`)
- **Purpose**: the mechanism by which Wikidata/ConceptNet/Arabic WordNet (Part 3 §6) and spell-correction dictionaries (Part 3 §7) get from raw downloaded data into `IKnowledgeGraph`'s storage. Full design is Part 8's deliverable; this Part only fixes its place in the dependency graph (Infrastructure layer, feeding Knowledge Graph storage, never called at request time).

---

## 3. Dependency Rules

```
Business Layer            (AiSearchAppService, ReportAppService, ReportMatchingBackgroundJob)
      ↓
Semantic Services          (IQueryPipeline and its direct collaborators, §2.1–§2.8)
      ↓
Abstractions                (every interface in §7 — the ONLY thing Semantic Services may
                              reference from the layers below)
      ↓
Infrastructure              (ONNX Runtime host, IVectorIndex implementation, IDistributedCache-
                              backed IQueryProcessingCache, IDatasetImporter runners)
      ↓
AI Providers / Local Models (Gemini/OpenAI/Ollama/HuggingFace/DeepSeek adapters, local ONNX
                              BGE-M3 model)
```

**Rule, stated precisely**: no class above the Abstractions line may reference a concrete class below it. This is the rule Part 2 found violated five separate times (`ConfidenceCalibrator`, `ObjectTypeRelationship`, `SearchTextProcessor`, `MatchExplanationGenerator`, `QueryProcessingCache` all referenced statically, skipping the Abstractions layer entirely) — §7's interface inventory is what makes the rule enforceable going forward.

No circular dependencies are introduced — the dependency graph remains a strict DAG, same shape as Part 2 §4.2 confirmed for the current code, just with more (properly abstracted) layers.

---

## 4. Folder Structure

Per `CLAUDE.md`: no new top-level folders, no renamed/moved existing folders. Every new component lives **inside the existing `AI/` folder**, which `CLAUDE.md` already designates as "the primary implementation location" for semantic engine components. `AI/Providers/` (existing) is untouched in location; everything else is new subfolders within `AI/`.

```
AI/
├── AiMatchingService.cs                 (retained; becomes IQueryPipeline's thin composition,
│                                          most current logic moves into the folders below)
├── AiSearchAppService.cs                (unchanged)
├── AIProviderOptions.cs                 (unchanged, gains IValidateOptions per Part 2 §11)
├── LostFoundAiProvidersServiceCollectionExtensions.cs  (unchanged location; internals updated
│                                          for provider registry + resilience decorator, §2.9)
│
├── Abstractions/                        (NEW — every interface in §7)
│   ├── IQueryPipeline.cs
│   ├── ILanguageDetector.cs
│   ├── ITextNormalizer.cs
│   ├── ISpellCorrector.cs
│   ├── IKnowledgeGraph.cs
│   ├── IConceptResolver.cs
│   ├── ISemanticExpander.cs
│   ├── IEmbeddingEngine.cs
│   ├── IVectorIndex.cs
│   ├── IHybridSearchEngine.cs
│   ├── IScoreComponent.cs
│   ├── IRankingEngine.cs
│   ├── IConfidenceCalibrator.cs
│   ├── IMatchExplanationService.cs
│   ├── IQueryProcessingCache.cs
│   └── IDatasetImporter.cs
│
├── Language/                            (NEW — §2.2 implementations)
│   ├── ScriptRangeLanguageDetector.cs
│   ├── ArabicTextNormalizer.cs / UrduTextNormalizer.cs / EnglishTextNormalizer.cs
│   └── SymSpellCorrector.cs
│
├── Knowledge/                           (NEW — §2.3 implementations; full design in Part 5)
│   ├── KnowledgeGraph.cs
│   ├── ConceptResolver.cs
│   └── SemanticExpander.cs
│
├── Embeddings/                          (NEW — §2.4 implementations; full design in Part 6)
│   ├── OnnxEmbeddingEngine.cs
│   └── ProviderFallbackEmbeddingEngine.cs  (wraps existing AI/Providers/* as the fallback tier)
│
├── Search/                              (NEW — §2.5 implementations; full design in Part 7)
│   ├── HnswVectorIndex.cs
│   ├── Bm25LexicalScorer.cs
│   └── HybridSearchEngine.cs
│
├── Ranking/                             (NEW — §2.6 implementations)
│   ├── RankingEngine.cs
│   └── ScoreComponents/
│       ├── TextSimilarityScoreComponent.cs
│       ├── ObjectTypeScoreComponent.cs
│       └── ... (one file per existing/future signal)
│
├── Confidence/                          (NEW — §2.7)
│   └── PiecewiseLinearConfidenceCalibrator.cs
│
├── Explanation/                         (NEW — §2.8)
│   ├── MatchExplanationService.cs
│   └── Resources/  (per-language templates)
│
├── Caching/                             (NEW — §2.4's IQueryProcessingCache implementation)
│   └── DistributedQueryProcessingCache.cs
│
├── Importers/                           (NEW — §2.10; full design in Part 8)
│   ├── WikidataImporter.cs
│   ├── ConceptNetImporter.cs
│   └── ArabicWordNetImporter.cs
│
├── Diagnostics/                         (NEW — Part 7's Observability design)
│   └── (pipeline timing/metrics instrumentation)
│
└── Providers/                            (EXISTING — untouched location; gains a resilience
                                            decorator and registry per §2.9)
    └── ... (all 14 existing files, unchanged in place)
```

This is a **subfolder-only** reorganization inside a folder `CLAUDE.md` already names as the correct location — no existing file is moved, renamed, or relocated outside `AI/`; `BackgroundJobs/` and `Reports/` are untouched.

---

## 5. Runtime Flow

### 5.1 Query Search (replaces Part 2 §4.4's "Search request" flow)
```
User Query
   → ILanguageDetector.Detect            (script + Ar/Ur disambiguation)
   → ITextNormalizer.Normalize           (per detected language)
   → ISpellCorrector.Correct             (SymSpell, per language)
   → IConceptResolver.Resolve            (words → Concept IDs — replaces SynonymMap)
   → ISemanticExpander.Expand            (Concept → related Concepts — replaces ObjectTypeRelationship)
   → IEmbeddingEngine.GenerateEmbeddingAsync   (local ONNX first, provider fallback second)
   → IHybridSearchEngine.SearchAsync     (BM25 + IVectorIndex, fused via RRF → shortlist)
   → IRankingEngine.Score                (existing CandidateScore logic, now componentized)
   → IConfidenceCalibrator.Calibrate     (unchanged piecewise-linear remap)
   → IMatchExplanationService.Build      (unchanged deterministic explanation, now multilingual)
   → Final Results
```

### 5.2 Report Classification (background job path)
Unchanged at the orchestration level (`ReportMatchingBackgroundJob`, Part 2 §2.11) except: (a) classification still goes through `IItemClassificationProvider` (external — no local classification model was selected in Part 3, this remains provider-based), (b) the text embedding call goes through the new `IEmbeddingEngine` (local-first) instead of directly through `IEmbeddingProvider`, (c) logging severity is corrected from `LogCritical` to `Information`/`Debug` per Part 2 §11's cheap-win recommendation.

### 5.3 Embedding Generation
```
Text → IEmbeddingEngine
          ├── local ONNX session available? → run locally (BGE-M3) → return
          └── else → fall back to IEmbeddingProvider chain (existing Gemini/OpenAI/Ollama/
                      HuggingFace, now wrapped by the resilience decorator) → return
```
This is the concrete implementation of Part 1's "Local-First Strategy" priority ladder (Local Embeddings before External AI) — today's code has no local tier at all (Part 2 §1).

### 5.4 Match Detection
`IHybridSearchEngine` produces the shortlist; `IRankingEngine` scores it — structurally identical to today's `ScoreCandidates`/`BuildCandidateScore` flow (Part 2 §2.1), just no longer a full linear scan of every candidate (§2.5's `IVectorIndex` bounds the shortlist first).

### 5.5 Explanation Generation
Unchanged in spirit from today (Part 2 §2.6's strength is preserved): purely local, deterministic, no AI call, now template+resource driven instead of per-language hand-written methods.

### 5.6 Provider Fallback
```
IEmbeddingEngine / IItemClassificationProvider call
   → attempt local (embedding only — no local classification model exists yet)
   → attempt primary external provider (resilience-decorated: retry/backoff/circuit-breaker)
   → on exhaustion, degrade gracefully exactly as AiMatchingService.ClassifySearchAsync does
     today (Part 2 §2.1) — empty classification, raw-text-only search — this fallback
     behavior is explicitly preserved, not redesigned
```

---

## 6. Service Responsibilities

| Service | Owns | Does NOT own |
|---|---|---|
| `IQueryPipeline` | Stage sequencing only | Any scoring math, any AI call detail |
| `ILanguageDetector`/`ITextNormalizer`/`ISpellCorrector` | Text-level linguistic processing | Concept/semantic meaning |
| `IKnowledgeGraph`/`IConceptResolver`/`ISemanticExpander` | Concept storage, resolution, expansion | Embeddings, ranking |
| `IEmbeddingEngine` | Text/image → vector, local-vs-provider routing | Vector storage/search |
| `IHybridSearchEngine`/`IVectorIndex` | Candidate generation (recall) | Final precise scoring (precision) |
| `IRankingEngine`/`IScoreComponent` | Final candidate scoring (precision) | Candidate generation (recall) |
| `IConfidenceCalibrator` | Display-score remapping only | Ranking order (must never change it — same invariant as today) |
| `IMatchExplanationService` | Natural-language description of an already-final score | Any scoring |
| Provider adapters | External AI I/O only | Business rules, scoring |
| `IDatasetImporter` | Offline data → Knowledge Graph storage | Runtime query serving |

This table is the direct answer to Part 2's repeated SRP findings — each row is one of the responsibilities `AiMatchingService` currently conflates.

---

## 7. Interface Inventory

| Interface | Why it exists |
|---|---|
| `IQueryPipeline` | Top-level replacement for `AiMatchingService`'s orchestration role — the seam business logic depends on. |
| `ILanguageDetector` | Makes language detection swappable/testable; closes the Arabic/Urdu ambiguity gap Part 3 §7 identified. |
| `ITextNormalizer` | Per-language normalization without editing a shared static class per new language. |
| `ISpellCorrector` | Introduces a capability that does not exist at all today. |
| `IKnowledgeGraph` | The single most important new abstraction — replaces two separate hardcoded tables (`ObjectTypeRelationship`, `SynonymMap`) with one queryable, data-driven source of truth. |
| `IConceptResolver` | Word-to-concept resolution, decoupled from the graph's storage details. |
| `ISemanticExpander` | Concept-to-related-concepts expansion, decoupled from the graph's storage details. |
| `IEmbeddingEngine` | Introduces the local-first routing that does not exist today (Part 2 §1's central finding). |
| `IVectorIndex` | Makes candidate generation swappable (HNSW today, something else later) without touching ranking. |
| `IHybridSearchEngine` | Encapsulates the BM25+dense+RRF fusion (Part 3 ADR-3) behind one call. |
| `IScoreComponent` / `IRankingEngine` | Turns the OCP-violating `ScoringWeights`/`BuildCandidateScore` pair into an extensible, testable set of small units. |
| `IConfidenceCalibrator` | Preserves today's proven calibration behavior while making it swappable/configurable. |
| `IMatchExplanationService` | Preserves today's proven local-explanation behavior while making it genuinely multilingual. |
| `IQueryProcessingCache` | Makes the query cache swappable (in-memory today, distributed later) — closes Part 2 §2.8's horizontal-scalability gap. |
| `IDatasetImporter` | One contract every knowledge-source importer (Wikidata, ConceptNet, Arabic WordNet, …) implements, per Part 8. |

---

## 8. Extension Strategy

- **New language**: add a normalization/spell-correction dataset + register it. No code change to `IQueryPipeline` or any consuming class.
- **New provider**: implement `IItemClassificationProvider`/`IEmbeddingProvider` and register via the provider registry (§2.9) — no edit to existing registration code.
- **New scoring signal** (e.g., finally implementing `LocationBonus`/`DateBonus`, still stubbed at zero today): implement `IScoreComponent`, register it — no edit to `IRankingEngine`.
- **New knowledge source**: implement `IDatasetImporter`, run it offline — no runtime code change, no redeploy required for the data itself (only for the importer, once).
- **Swap embedding model**: configuration change to `IEmbeddingEngine`'s ONNX model path — no code change.
- **Swap vector index implementation**: implement `IVectorIndex` — no change to `IHybridSearchEngine` or anything above it.

Every extension path above requires touching **one new file and one registration line**, never an existing class's internals — this is the concrete, verifiable form of Part 1 Principle 5 (Extensibility).

---

## 9. Architecture Decision Records (ADR)

**ADR-7 — Modularize in place, do not rewrite the process boundary.**
*Decision*: keep one .NET process, one ABP module; introduce internal seams (interfaces + subfolders under `AI/`) rather than splitting into microservices.
*Alternatives considered*: separate "Semantic AI" microservice/API.
*Why*: `CLAUDE.md` mandates no new top-level architecture and no parallel architectures; Part 3 ADR-1 already rejected new infrastructure services for the vector index specifically, and the same reasoning applies to the platform as a whole at current scale.

**ADR-8 — Abstractions layer is mandatory between Semantic Services and Infrastructure.**
*Decision*: no class may statically reference `AI/Language`, `AI/Knowledge`, `AI/Embeddings`, `AI/Search`, `AI/Ranking`, `AI/Confidence`, `AI/Explanation`, or `AI/Caching` implementation types — only their `AI/Abstractions` interfaces.
*Alternatives considered*: keep today's static-class pattern for the "cheap, obviously deterministic" components (calibration, explanation) since they have no real polymorphic need yet.
*Why rejected*: this is exactly the reasoning that produced the current five DIP violations (Part 2 §5) — "it doesn't need to be swappable yet" was true right up until multilingual support, configurable calibration, and testability all turned out to need it. The interface costs one file each; the alternative cost was a full Part 2 review's worth of findings.

**ADR-9 — Local-first embedding routing lives inside `IEmbeddingEngine`, not in `IQueryPipeline`.**
*Decision*: the pipeline calls one `IEmbeddingEngine.GenerateEmbeddingAsync`; local-vs-provider fallback logic is entirely internal to that implementation.
*Alternatives considered*: pipeline explicitly tries local then provider itself.
*Why*: keeps `IQueryPipeline` a pure sequencer (§2.1) and keeps the local/provider decision colocated with the thing that knows how to make it — consistent with the Service Responsibilities table (§6).

**ADR-10 — Candidate generation (recall) and ranking (precision) are two separate interfaces, never merged.**
*Decision*: `IHybridSearchEngine` and `IRankingEngine` are distinct, sequential stages.
*Alternatives considered*: one `ISearchEngine` doing both.
*Why*: today's code already implicitly separates "fetch candidates" (`GetSearchableReportsAsync`) from "score candidates" (`ScoreCandidates`) — Part 4 formalizes an already-sound instinct rather than merging two concerns that the current code correctly keeps apart.

---

## 10. Migration Alignment with Phase 2

- **Phase 2A** builds: `AI/Abstractions/*`, `AI/Language/*`, `AI/Knowledge/*` (per Part 5), `AI/Embeddings/*` (per Part 6), `AI/Importers/*` (per Part 8), and the provider-registry/resilience-decorator changes to existing `AI/Providers/*` — i.e., the **foundation layers** (bottom four rows of §1's diagram), which can be built and tested without touching `AiMatchingService`'s external behavior at all.
- **Phase 2B** builds: `AI/Search/*` (per Part 7), `AI/Ranking/*`, `AI/Confidence/*`, `AI/Explanation/*`, and finally the cut-over of `AiMatchingService`/`IQueryPipeline` itself to consume the new modules — i.e., the **pipeline layer**, which does change external behavior (search quality, latency) and is where Part 9's rollout/rollback plan applies most directly.
- This ordering — infrastructure and abstractions first, pipeline cut-over last — is what makes each Phase 2A stage independently verifiable (per Part 1 Principle: every implementation phase has clear direction) without a single "big bang" replacement of `AiMatchingService`.

*End of Part 4 deliverable. No production code was written or modified.*
