# PHASE 1 — Part 6 (Deliverable)
# Local AI Stack, Embedding Strategy & Inference Architecture

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-6-Local-AI-Stack-Embedding-Strategy.md`.
> Deepens Part 3's technology selection (§3, §8) into a concrete lifecycle/storage/optimization design for the `IEmbeddingEngine` introduced in Part 4 §2.4. Architecture only — no production code was written.

---

## 1. Embedding Model Evaluation (Deepened from Part 3 §3)

| Model | Arabic | English | Urdu | Cross-lingual retrieval | Model size (fp32 / int8) | CPU latency (single short query) | ONNX | Quantization | License |
|---|---|---|---|---|---|---|---|---|---|
| **BGE-M3** | Strong | Strong | Supported | Strong — trained explicitly for multilingual + cross-lingual retrieval | ~2.2GB / ~560MB | Moderate (larger model; still sub-second on modern CPU for short queries) | Yes, official + community | Yes (int8, well-documented) | MIT |
| multilingual-e5-large | Strong | Strong | Supported | Strong | ~2.2GB / ~560MB | Moderate | Yes | Yes | MIT |
| multilingual-e5-base | Good | Good | Supported | Good | ~1.1GB / ~280MB | Fast | Yes | Yes | MIT |
| Jina Embeddings v3 | Good | Strong | Limited/unverified | Good | ~2.2GB | Moderate | Yes | Yes | **CC-BY-NC — excluded per Part 3 §3, licensing unresolved** |
| Nomic Embed | Moderate | Strong | Weak | Moderate | ~550MB | Fast | Yes | Yes | Apache 2.0 |
| GTE (multilingual) | Good | Strong | Limited | Good | Varies | Fast-moderate | Yes | Yes | Apache 2.0 |
| Sentence-Transformers (generic multilingual) | Moderate | Good | Moderate | Moderate | ~1.1GB | Fast | Yes | Yes | Apache 2.0 |

This table restates Part 3 §3 with the additional CPU/quantization detail this Part's mandate requires; the conclusion is unchanged.

## 2. Final Model Recommendation

- **Primary: BGE-M3, int8-quantized ONNX export.** Chosen for: (a) native multi-functionality — dense, sparse (lexical-weighted), and ColBERT-style multi-vector output from **one** model, which directly feeds Part 3 ADR-3's hybrid-retrieval design without needing a second sparse-scoring model; (b) genuinely multilingual training coverage including Arabic and Urdu, not an afterthought; (c) MIT license, zero commercial-use friction; (d) mature ONNX export path.
- **Fallback: multilingual-e5-base, int8-quantized ONNX export.** Used when: the primary model isn't loaded yet (cold-start grace period), memory constraints require the smaller footprint, or a specific deployment profile explicitly opts for lower latency over BGE-M3's fuller feature set (e.g. sparse/multi-vector unused). Selected as fallback specifically because it shares BGE-M3's license family and general architecture lineage, minimizing behavioral surprise when the fallback activates.
- Both are selected, not just one, because Part 1 Principle 4 (Replaceability) requires the embedding model to be swappable — building the fallback path now (rather than retrofitting it later) validates that the abstraction (`IEmbeddingEngine`, Part 4 §2.4) actually supports swapping from day one.

## 3. Inference Runtime Recommendation

**ONNX Runtime** (`Microsoft.ML.OnnxRuntime` NuGet package), per Part 3 ADR-4, restated here with the specifics this Part requires:

| Concern | ONNX Runtime answer |
|---|---|
| Deployment simplicity | Single NuGet reference; no native toolchain, no Python runtime dependency in production |
| CPU performance | Graph-level optimizations (operator fusion, constant folding) + explicit INT8 quantization support via its own quantization tooling |
| Startup time | Model load is the dominant cost (hundreds of ms to a few seconds depending on model size) — addressed by singleton lifecycle + lazy/background warm-up (§4, §7) |
| Memory footprint | Reduced materially by int8 quantization (roughly 4x smaller than fp32) |
| Portability | Windows/Linux/macOS, x64/ARM64 — matches "deploy anywhere, offline-capable" |
| Concurrency | `InferenceSession.Run` is documented safe for concurrent calls against one shared session — no per-request session creation needed, no external locking needed |

---

## 4. Embedding Lifecycle Design

```
Dataset (report text, or Knowledge Graph concept text, Part 5)
   ↓
Embedding Generation        (IEmbeddingEngine → local ONNX BGE-M3, or provider fallback)
   ↓
Validation                  (non-empty vector, correct dimensionality, no NaN/Inf components —
                              cheap, mandatory sanity check before persistence; a silently-corrupt
                              embedding is worse than a missing one, since it would score plausibly
                              but wrongly)
   ↓
Versioning                  (embedding tagged with ModelId + ModelVersion — see §6; never mixed
                              with vectors from a different model/version in the same index)
   ↓
Compression                 (int8/float16 storage representation where the index format supports it,
                              distinct from the model's own quantization in §3 — this is storage-level,
                              not inference-level)
   ↓
Persistence                 (report's embedding column, as today, PLUS an entry in the vector index,
                              Part 4 §2.5's IVectorIndex)
   ↓
Index Build                 (incremental add to the live IVectorIndex — never a full rebuild per
                              report; full rebuild is reserved for model/version upgrades, §6)
   ↓
Serving                     (IHybridSearchEngine reads the index at query time, Part 7)
   ↓
Incremental Updates         (report edited → re-embed → replace-in-place in both the persisted
                              column and the index — mirrors ReportAppService.UpdateAsync's existing
                              change-detection re-trigger, Part 2 §2.12, which is preserved unchanged)
```

**"Embeddings must never be regenerated unnecessarily"** (spec's explicit constraint) is satisfied by: (a) the existing change-detection logic in `ReportAppService.UpdateAsync` (Part 2 §2.12) that only re-triggers the background job when description/image/location actually changed — kept as-is; (b) `IQueryProcessingCache` (Part 4 §2.4) continuing to cache query-side embeddings so an identical repeated search never re-embeds; (c) model/version-tagged storage (§6) meaning a model upgrade only forces re-embedding of *existing* candidates once, in a controlled batch job, not on every read.

---

## 5. Embedding Storage

Restating and extending Part 3 §2's vector-index decision with embedding-specific storage detail:

| Option | Lookup latency | Startup speed | Scalability | Backup | Version mgmt |
|---|---|---|---|---|---|
| Raw binary files per report | Poor without an index | Fast (no parse) | Poor | Simple (file copy) | Manual |
| SQLite / SQL Server (existing pattern — today's `EmbeddingJson` column) | Poor for similarity search without an index; fine for row-level fetch | Fast | Moderate | Simple (DB backup) | Straightforward (schema-versioned columns) |
| pgvector | Good, native `ORDER BY embedding <-> query` | Fast | Good at this project's scale | Simple (DB backup) | Straightforward | 
| FAISS / HNSW in-process index | Excellent | Requires index build/load at startup (§4) | Good at this project's scale | Requires explicit index serialization | Requires explicit handling (§6) |
| Qdrant / Weaviate | Excellent | N/A (separate service) | Excellent at large scale | Built-in | Built-in | 

**Recommendation** (consistent with Part 3 §2 and Part 5 §8's storage philosophy — one consistent pattern platform-wide): the **persisted column remains the durable source of truth** (as today — `Report.EmbeddingJson`/`ImageEmbeddingJson`, unchanged schema), while the **in-process `IVectorIndex` (HNSW/FAISS) is a rebuildable-from-source cache** for fast similarity search, built at startup and incrementally maintained thereafter. If the ABP data provider is confirmed as PostgreSQL, pgvector remains the documented zero-new-infrastructure alternative that could replace the in-process index specifically, without touching the durable-storage decision.

---

## 6. Model Management

- **Model discovery**: models are shipped as versioned ONNX files under a configured model directory (not downloaded at runtime — consistent with offline-first); `IEmbeddingEngine`'s configuration names the active model file + declares its `ModelId`/`ModelVersion`.
- **Version tracking**: every stored embedding (persisted column and index entry alike) carries its `ModelId`/`ModelVersion`. A query embedding generated by a different model/version than a candidate's stored embedding must never be compared directly — the engine either re-embeds the candidate on read (expensive, avoid) or the platform runs a **controlled batch re-embedding job** after a model upgrade before the new model is allowed to serve production traffic against old vectors. This is the mechanism that makes "embeddings must never be regenerated unnecessarily" (§4) compatible with "the model must be replaceable" (Part 1 Principle 4) — regeneration only ever happens as one deliberate, versioned batch operation, never silently per-request.
- **Integrity validation**: model file checksum verified at load time; load fails loudly (not silently falling back to a stale/corrupt model) if the checksum doesn't match the configured expectation.
- **Lazy loading**: the ONNX session loads on first use or on a background warm-up task at application startup (configurable) — avoids blocking application startup on model load while still avoiding a cold-start penalty on the very first user request if warm-up is enabled.
- **Health monitoring**: a lightweight self-check (embed a fixed known string at startup, verify output dimensionality/non-NaN) surfaces a broken model file as a startup health-check failure rather than a mysterious first-request error.
- **Future hot swapping**: the versioning scheme (above) is what makes a future zero-downtime model swap possible — load the new model's session alongside the old, cut traffic over once its batch re-embedding job completes, retire the old session. Not implemented in Phase 2A/2B, but the versioning groundwork here is what prevents it from being a breaking change later.

---

## 7. Multilingual Strategy

One embedding space serves Arabic, English, and Urdu (and, per Part 1, is extensible to Hindi/Turkish/Persian/Malay/French) because BGE-M3/multilingual-e5 are trained specifically for cross-lingual retrieval — a query in one language and a candidate description in another land close together in vector space **without a translation step**. This is validated conceptually by Part 5 §5's Concept-graph design (same `ConceptId` across languages) and Part 3 §3's model selection criteria; it is not re-derived here, only confirmed as the reason a single shared `IVectorIndex` (not one per language) is correct.

---

## 8. Optimization Strategy

| Technique | Application here |
|---|---|
| Quantization | int8 ONNX export for both primary and fallback models (§2) — the single highest-leverage optimization for CPU latency and memory. |
| Batch inference | `ReportMatchingBackgroundJob` embeds one report at a time today (Part 2 §2.11) — acceptable for per-report background processing (not latency-sensitive), but the Part 8 dataset-import pipeline (bulk Knowledge Graph concept embedding) should batch requests to the ONNX session rather than looping one-at-a-time, since batch inference amortizes fixed per-call overhead. |
| Memory mapping | The `IVectorIndex` (§5) uses a memory-mapped on-disk representation where the chosen library supports it, so index load doesn't require materializing the full structure in managed heap memory. |
| SIMD | Delegated entirely to ONNX Runtime's own CPU execution provider (which already uses SIMD internally) — no custom SIMD code needed in application code. |
| Lazy initialization | §6's lazy/background model loading. |
| Embedding cache | `IQueryProcessingCache` (Part 4 §2.4/§8) continues to cache query-side embeddings; candidate-side embeddings are cached implicitly by being resident in the `IVectorIndex` rather than re-decoded from JSON per request — directly closing Part 2 §6's "no caching of decoded candidate embedding vectors" finding. |
| CPU affinity | ONNX Runtime session options expose intra-op/inter-op thread count configuration — set explicitly (rather than left at library defaults) once real load-testing data exists in Phase 2A/2B, not guessed at in Phase 1. |
| Parallel execution | Candidate scoring (Part 2 §7's "no parallelization" finding) is addressed in Part 4/7's ranking design, not here — this Part only ensures the embedding engine itself doesn't become the serialization point (ONNX Runtime's documented concurrent-session-use safety, §3, is what enables that). |

---

## 9. Integration Strategy

| Consumer | Integration point |
|---|---|
| Knowledge Graph (Part 5) | Each Concept's optional `EmbeddingReference` (Part 5 §2.1) is generated by the same `IEmbeddingEngine`, embedding the concept's canonical description — enabling concept-to-concept similarity queries using the identical model/vector-space as report matching, so "semantically close concepts" and "semantically close reports" are comparable notions, not two disconnected systems. |
| Hybrid Search (Part 4 §2.5, detailed in Part 7) | `IHybridSearchEngine` calls `IEmbeddingEngine` for the query vector, then queries `IVectorIndex` for nearest candidates — the dense half of the BM25+dense fusion. |
| Ranking Engine (Part 4 §2.6) | Consumes the already-computed text/image similarity scores from the hybrid search stage — does not call `IEmbeddingEngine` directly, preserving the Service Responsibilities separation Part 4 §6 defines. |
| Confidence Engine (Part 4 §2.7) | No direct dependency — operates purely on the final raw score, unchanged from today. |
| AI Providers (existing) | Retained as `IEmbeddingEngine`'s fallback tier (Part 4 §2.4/§5.3) — local embeddings are primary, external providers are the safety net when local inference is unavailable/misconfigured, exactly inverting today's provider-only default. |

Local embeddings remain the **primary** semantic source in every one of these integration points, per Part 1's Local-First Strategy — external providers are never the first call once Phase 2A ships.

---

## 10. Risk & Trade-off Analysis

- **Model size vs. quality (BGE-M3 vs. e5-base)**: BGE-M3's larger footprint costs startup time and memory; mitigated by int8 quantization and by keeping e5-base as a genuinely usable fallback, not a theoretical one.
- **Local inference correctness risk**: a bug in local embedding generation is now the platform's own responsibility, whereas today a provider bug is externally owned. Mitigated by §6's health-check-at-load and by keeping the provider fallback path live (not removed) through at least Phase 2A/2B, so local inference failures degrade to the previously-working provider path rather than to nothing.
- **Model/version drift risk**: comparing vectors from two different model versions produces meaningless similarity scores. Mitigated entirely by §6's mandatory version tagging + controlled batch re-embedding on upgrade — this is called out explicitly because it is the one embedding-lifecycle mistake that fails *silently* (a low-but-plausible-looking score) rather than loudly, making it the highest-priority invariant to enforce in implementation.
- **Cold-start latency risk**: first request after deployment could stall on model load if warm-up isn't configured. Mitigated by §6's optional background warm-up at startup.
- **Licensing risk**: closed by Part 3 §3's exclusion of Jina v3 pending license verification — both selected models are MIT-licensed with no redistribution restriction.

---

## 11. Future GPU Migration Plan

Not needed for Phase 2A/2B (CPU-first is the stated NFR), but designed for without redesign later:

1. ONNX Runtime supports a CUDA/DirectML execution provider as a drop-in swap of the execution provider configuration — no model format change, no `IEmbeddingEngine` interface change.
2. The versioning scheme (§6) already isolates "which model produced this vector" from "which hardware ran it" — a GPU-executed BGE-M3 produces vectors identical (within floating-point tolerance) to a CPU-executed one, so no re-embedding is forced by a GPU migration alone, only by an actual model change.
3. Batch inference (§8) becomes considerably more valuable on GPU (higher fixed-cost-per-call amortization benefit) — the dataset-import pipeline's batching design (§8) is therefore GPU-migration-friendly by construction, not something to redo later.
4. TensorRT (Part 3 §8) remains the documented option **if** a specific deployment target has NVIDIA hardware and the operational complexity is justified by measured CPU-inference bottlenecks — not adopted speculatively.

*End of Part 6 deliverable. No production code was written or modified.*
