# Phase 2A Part 5 — Production Operations Guide

> Companion documentation to `SemanticReports/PHASE-2A-PART-5-Infrastructure-Storage-Caching-Report.md`, satisfying Phase 2A Part 5's "Production documentation" deliverable.
>
> **Revision note**: an earlier version of this document was written before any of Phase 2A was implemented and described a hypothetical design (different config keys, a `metadata.db` file, `WikidataImporter`/`ConceptNetImporter` classes, etc.) that does not match what was actually built. This revision reflects the real, working implementation as of Phase 2A Part 5 — verified by an automated test suite (`modules/lostfound/test/LostFound.Application.Tests/AI/`), not by inspection alone.

---

## 1. Infrastructure Overview

The semantic-AI foundation (Phase 2A Parts 1–5) is a set of in-process .NET services added to the existing `LostFound.Application` module — **no new deployable service** is introduced. It reads/writes two local SQLite database files and a local model directory, all under configured paths:

```
{LocalRuntime:ModelDirectory}/            (default: AI-Models/Embeddings, relative to content root)
└── {ModelName}/{ModelVersion}/
    ├── model.onnx                        (LocalRuntime:ModelFileName)
    └── sentencepiece.bpe.model           (LocalRuntime:TokenizerFileName)

{LocalRuntime:DatabasePath}               (default: AI-Data/embeddings.db)
├── table: embedding_models               (model install/version history - IEmbeddingModelManager, IEmbeddingVersionManager)
└── table: embedding_cache                (persistent embedding vector cache - IEmbeddingStore)

{KnowledgeGraph:DatabasePath}             (default: AI-Data/knowledge.db)
├── table: concepts                       (IConceptRepository)
├── table: concept_history                (audit trail / rollback source)
├── table: concept_relationships          (IRelationshipRepository)
└── table: dataset_imports                (IDatasetImportHistoryRepository)
```

Everything here operates fully offline once a model is installed (Section 6); with no model installed, embedding generation transparently falls back to the configured external provider (Phase 2A Part 1), and everything else (knowledge graph, importers) already works fully offline today with no model required at all.

---

## 2. Configuration Reference

All keys live under `LostFound:AI`, alongside the existing provider configuration from Phase 2A Part 1:

```jsonc
{
  "LostFound": {
    "AI": {
      "Provider": "Gemini",
      "EmbeddingProvider": null,

      "LocalRuntime": {
        "Enabled": true,
        "ModelDirectory": "AI-Models/Embeddings",
        "DatabasePath": "AI-Data/embeddings.db",
        "ModelFileName": "model.onnx",
        "TokenizerFileName": "sentencepiece.bpe.model",
        "EmbeddingDimensions": 1024,
        "SupportedLanguages": [ "ar", "en", "ur" ],
        "InputIdsTensorName": "input_ids",
        "AttentionMaskTensorName": "attention_mask",
        "TokenTypeIdsTensorName": "token_type_ids",
        "OutputTensorName": "last_hidden_state",
        "MaxSequenceLength": 512,
        "MemoryCacheMaxEntries": 2000
      },

      "KnowledgeGraph": {
        "DatabasePath": "AI-Data/knowledge.db",
        "ConceptCacheMaxEntries": 5000
      }
    }
  }
}
```

Every field has a working default (shown above) — the whole `LocalRuntime`/`KnowledgeGraph` sections may be omitted entirely and the platform behaves exactly as Phase 2A Part 1 (provider-only), since `LocalRuntime.Enabled` and no installed model both independently make `IEmbeddingRuntime.IsAvailable` false. Both option classes are validated at first access (`LocalAiRuntimeOptionsValidator`, `KnowledgeGraphOptionsValidator` — Section 5) — an invalid value (blank path, non-positive dimension/entry-count) throws `OptionsValidationException` with an actionable message instead of failing silently or deep inside `OnnxEmbeddingModel`.

---

## 3. Storage

| Concern | Interface(s) | Backing | Notes |
|---|---|---|---|
| Embedding vectors | `IEmbeddingStore` (also `IVectorStore`) | SQLite, `embeddings.db` → `embedding_cache` | Keyed by `(content hash, model version)` |
| Model lifecycle | `IEmbeddingModelManager`, `IEmbeddingVersionManager` (also `IModelStore`, `IMetadataStore`) | SQLite, `embeddings.db` → `embedding_models` | Install/activate/rollback/checksum-verify |
| Concepts | `IConceptRepository` | SQLite, `knowledge.db` → `concepts` / `concept_history` | Full version/rollback audit trail |
| Relationships | `IRelationshipRepository` | SQLite, `knowledge.db` → `concept_relationships` | |
| Import history | `IDatasetImportHistoryRepository` (also `IMetadataStore`) | SQLite, `knowledge.db` → `dataset_imports` | |
| Knowledge graph facade | `IKnowledgeGraph` (also `IKnowledgeStore`) | Composes the two knowledge-graph repositories above | Graph traversal (BFS, bounded depth) |

`IVectorStore`/`IKnowledgeStore`/`IMetadataStore`/`IModelStore`/`ICacheStore` (the five names the Part 5 spec asks for) are declared as **empty marker interfaces**, additionally implemented by the concrete classes above, rather than as a second parallel set of CRUD interfaces — see `AI/Storage/StorageAbstractions.cs` for the reasoning. Each concern's *real* interface already satisfies "storage providers must be replaceable without changing business logic" (every consumer depends on the interface, never the concrete SQLite class) — a second redundant abstraction would not have added any actual replaceability.

Migrating any one store to different infrastructure (e.g. `IEmbeddingStore` to pgvector, per Phase 1 Part 6's documented escape hatch) means writing one new class implementing that store's interface and changing one DI registration line — no caller changes.

---

## 4. Caching

Two independent in-process caches, both bounded with FIFO eviction (a deliberately cheap, lock-free approximation of LRU — see each class's own remarks for why a full LRU wasn't built):

- `IEmbeddingCache` (`MemoryEmbeddingCache`) — fast path in front of `IEmbeddingStore`, checked before the local ONNX runtime is ever invoked.
- `IConceptCache` (`MemoryConceptCache`, via `CachedConceptRepository`) — read-through/write-through in front of `IConceptRepository.GetByIdAsync`, added in Part 5 to close a real gap (every concept lookup previously hit SQLite with no caching at all).

`IEmbeddingStore`/`IDatasetImportHistoryRepository`'s SQLite tables are themselves the **persistent disk cache** tier the spec's "Caching Strategy" section asks for — a separate generic disk-cache primitive was not built on top of them, since it would duplicate what those tables already do.

---

## 5. Diagnostics

- `IEmbeddingRuntimeDiagnostics.GetReportAsync()` (Phase 2A Part 2) — embedding runtime health, average/last inference latency, embedding cache size.
- `IAiPlatformDiagnostics.GetReportAsync()` (Phase 2A Part 5) — aggregates the above plus concept cache size, storage file health (exists / size / last-write time) for both SQLite databases, and the latest successful `DatasetImportRecord` per registered importer. This is the single entry point for an operator/dashboard/health-check endpoint.

Neither is wired to an ASP.NET Core `/health` endpoint yet — that's a small, low-risk integration step (register `IAiPlatformDiagnostics` inside a custom `IHealthCheck`) left for whoever owns the real deployment's health-check surface, since this module doesn't own `Program.cs`.

---

## 6. Deployment / Offline Installation

1. Deploy the application as normal — this platform adds no new deployable artifact, only NuGet package references (`Microsoft.ML.OnnxRuntime`, `Microsoft.ML.Tokenizers`, `Microsoft.Data.Sqlite`) already added to `LostFound.Application.csproj`.
2. Choose writable, backed-up paths for `LostFound:AI:LocalRuntime:DatabasePath`, `LostFound:AI:KnowledgeGraph:DatabasePath`, and `LostFound:AI:LocalRuntime:ModelDirectory` (defaults are relative to the app's working directory — override to absolute paths in production).
3. **Local embedding model installation** (one-time, requires an actual BGE-M3 or multilingual-e5 ONNX export + SentencePiece tokenizer file — not producible in this workspace, no internet access to the relevant model hosts):
   - Call `IEmbeddingModelManager.InstallAsync(descriptor)` with a real `EmbeddingModelDescriptor` (download URI + expected SHA-256 checksum) — the checksum is verified before the file is ever considered installed; a mismatch is rejected, not silently accepted.
   - Call `IEmbeddingModelManager.ActivateAsync(name, version)`.
   - Verify tensor I/O names (`InputIdsTensorName`/`AttentionMaskTensorName`/`OutputTensorName`) match the actual exported model (e.g. via Netron) before relying on it in production — a mismatch fails loudly at load time with a descriptive exception, not silently.
   - **Verify tokenization output against the real HuggingFace reference tokenizer** before trusting search quality — this is the single highest-severity correctness risk flagged throughout Phase 2A, and has not been verified against a real model in this environment (see the Part 2 report).
4. **Knowledge graph population**: the one real importer (`LostFoundSeedDataImporter`) runs automatically via `IImportCoordinator.ImportAllAsync` — no operator action needed for the built-in seed data. Adding a real ConceptNet/Wikidata/other importer means implementing `IDatasetImporter` and registering it in `AddLostFoundDatasetImporters` (`LostFoundImportersServiceCollectionExtensions.cs`).
5. No internet access is required by the running application after steps 3–4 complete — both are one-time, operator-triggered provisioning actions, never runtime calls.

---

## 7. Upgrade Process

- **Model upgrade**: `IEmbeddingModelManager.InstallAsync` a new version (old versions' history rows are never deleted), then `ActivateAsync` it. `IEmbeddingModelManager.RollbackToPreviousAsync()` reverts to the previously active version if needed. `LocalFirstEmbeddingEngine`'s cache/store keys embed the model version, so vectors from a retired model are never served as if they came from the new one.
- **Knowledge graph dataset upgrade**: re-run the relevant `IDatasetImporter` through `IImportCoordinator.ImportAsync(importer, ImportMode.Incremental)` — an unchanged `DatasetVersion` is automatically skipped (verified by an automated test); bump the source's version to force a real re-import.
- **Concept rollback**: `IConceptRepository.RollbackAsync(conceptId, version)` restores any archived version from `concept_history`, itself recorded as a new audited version rather than a silent overwrite.

---

## 8. Backup & Restore

Back up `embeddings.db`, `knowledge.db`, and the model directory. Restore is a direct file copy back into place — every store re-initializes its schema idempotently (`CREATE TABLE IF NOT EXISTS`) on first access, so no separate migration step is needed after restore.

---

## 9. Benchmark Methodology (documented protocol; not run against production-scale data)

The one real importer's seed dataset (26 concepts) is too small to produce meaningful throughput/latency numbers — these benchmarks are real, runnable code paths today, but a genuine benchmark run needs realistic data volume (Phase 2A Part 4's other 8 sources) to be meaningful:

| Metric | Method |
|---|---|
| Startup time | Time from process start to first `IEmbeddingRuntime.GetStatusAsync()` returning `Healthy` (cold model load), once a real model is installed. |
| Import duration | Wall-clock time for `IImportCoordinator.ImportAllAsync` — currently ~0.3-0.7s for the 26-concept seed set on this workspace's hardware; not representative of a real multi-thousand-concept source. |
| Cache hit ratio | `IEmbeddingCache.Count` / `IConceptCache.Count` growth over a realistic query replay, surfaced via `IAiPlatformDiagnostics`. |
| Storage latency | `IConceptRepository.GetByIdAsync`/`IEmbeddingStore.TryGetAsync` p50/p95 under concurrent load. |
| Memory usage | `GC.GetTotalMemory` before/after model load and during a sustained query workload. |

**Known scaling gap**: `ImportCoordinator` persists concepts/relationships one row at a time — correct for a ~26-concept seed set, but would need batched/bulk insert to meaningfully approach the spec's "millions of concepts" target. Not built, since nothing in this workspace exercises that scale yet (see the Part 4 report).
