# Phase 2B Part 4 — Migration Guide, Benchmark Report & Operational Runbook

> Extends `docs/Semantic-AI/Phase-2A/Deliverables/PART-5-Production-Operations-Guide.md` with everything specific to the Phase 2B query/retrieval/ranking pipeline and its integration into `AiMatchingService`. Satisfies Phase 2B Part 4's "Migration guide," "Benchmark report," and "Operational runbook" final deliverables.
>
> Unlike earlier drafts of this document, this version was written **after** implementation, build, and test execution — every claim below reflects what was actually built and actually verified in this workspace, not a plan for a future session to execute.

---

## 1. Migration Guide

### 1.1 Current state entering (and now exiting) Phase 2B Part 4

- `AiMatchingService.FindSimilarReportsAsync` branches at its very first line on `LostFound:AI:HybridPipeline:Enabled` (`HybridPipelineOptions`, default `false`).
- **Disabled (default, the live behavior today)**: execution is byte-for-byte identical to pre-Phase-2B — `SearchTextProcessor`, `ObjectTypeRelationship`, the static `ConfidenceCalibrator`/`MatchExplanationGenerator`, and `QueryProcessingCache` all still run exactly as before, untouched by this Part. `AiSearchAppService` and every DTO are unchanged either way.
- **Enabled**: text-only searches (`imageBytes == null` and `searchText` non-blank) route through `ISemanticSearchOrchestrator`, which composes `IQueryPipeline → IHybridSearchEngine → IRankingEngine → IObjectTypeCompatibilityService` (Phase 2B Parts 1-3 plus this Part's new ontology-driven object-type adjustment) into the same `RankedReportResult` shape the legacy path returns. Image searches, and text-only searches with the flag on but a null/blank `searchText`, always fall through to the legacy path — the new pipeline is text-only by design (`IQueryPipeline.ProcessAsync(string rawText, ...)` takes no image parameter).

### 1.2 New Part 4 components

| Component | Location | Purpose |
|---|---|---|
| `HybridPipelineOptions` | `Application/AI/Configuration/` | The `Enabled` flag plus retrieval-overfetch and object-type-penalty tuning, bound from `LostFound:AI:HybridPipeline`. |
| `ISemanticSearchOrchestrator` / `SemanticSearchOrchestrator` | Contracts + Application `AI/Integration/` | The new live search path: runs the query pipeline, overfetches candidates from `IHybridSearchEngine` (ranking needs more to work with than the caller's final `maxResults`), ranks them, applies the object-type adjustment, and maps to `RankedReportResult`. |
| `IObjectTypeCompatibilityService` / `ObjectTypeCompatibilityService` | Contracts + Application `AI/Ontology/` | The ontology-driven replacement for the legacy static `ObjectTypeRelationship` table, scoped to the new pipeline only — resolves both object-type strings to `Concept`s via `IConceptResolver` and classifies the pair as `Same`/`RelatedCluster`/`UnrelatedCluster`/`Unknown` by walking `IKnowledgeGraph.GetRelatedConceptsAsync` outward from **both** sides (the graph only exposes source→target traversal, and IsA edges are conventionally recorded child→parent, so a parent query would otherwise never reach a child candidate). |
| `ISearchAnalyticsRecorder` / `InMemorySearchAnalyticsRecorder` | Contracts + Application `AI/Analytics/` | Real, thread-safe, in-memory aggregation of every search (both pipelines): total/hybrid/legacy volume, average and P95 latency, zero-result rate, language distribution. Resets on process restart by design — this is live-traffic observability, not a persisted audit log. |
| `ISearchQualityMetricsCalculator` / `SearchQualityMetricsCalculator` | Contracts + Application `AI/Analytics/` | Precision@K, Recall@K, MAP, NDCG, MRR as pure functions of a ranked ID list and caller-supplied relevance judgments. Verified against textbook examples (`SearchQualityMetricsCalculatorTests`) — **not** run against real search traffic, because no labeled relevance dataset exists in this project (see §3). |

`IAiPlatformDiagnostics` (Phase 2A Part 5) already satisfies the spec's subsystem-health "Monitoring" deliverable (embedding runtime, Knowledge Store, Embedding Store status) — nothing new was needed for that half of monitoring; `ISearchAnalyticsRecorder` is specifically the search-*traffic* half.

### 1.3 Recommended rollout sequence for a real deployment

1. **Merge with the flag left at its default (`false`)** — zero behavior change. This step is already satisfied: the full solution builds with 0 errors/0 warnings and the flag defaults to `false` in `HybridPipelineOptions`.
2. **Enable the local embedding runtime** (`LostFound:AI:LocalRuntime:Enabled`, Phase 2A Part 2) once a real ONNX embedding model is installed — this workspace never had network access to a model host, so `LocalEmbeddingRuntimeTests` verify the graceful "not installed, fall back to the configured provider" path, not actual local inference.
3. **Import a real ontology** beyond the small curated Lost & Found seed dataset (Phase 2A Part 4) — both `IHybridSearchEngine`'s graph retrieval and this Part's `IObjectTypeCompatibilityService` degrade honestly to "no signal" / `Unknown` against an under-populated graph, they don't fail, but they also don't help until real data exists.
4. **Enable `LostFound:AI:HybridPipeline:Enabled` in a non-production environment first.** Compare `ISearchAnalyticsRecorder` snapshots (zero-result rate, latency) between the two paths under real traffic before touching production.
5. **Build a labeled relevance dataset** (§3) and run `ISearchQualityMetricsCalculator` against both paths before making any quantitative "the new pipeline is better" claim.
6. **Enable in production**, watching the same `ISearchAnalyticsRecorder` snapshot against the legacy path's historical baseline.
7. **Only after a soak period with no regression**, consider removing the legacy path and the four superseded static classes (`ObjectTypeRelationship`, the static `ConfidenceCalibrator`/`MatchExplanationGenerator`, `QueryProcessingCache`) — explicitly **not** done in Phase 2A/2B, per `CLAUDE.md`'s "prefer deprecation over deletion" and "verify nothing depends on it" rules. This is a deliberate future decision for whoever operates the real solution with real production data, not something to do speculatively here.

### 1.4 Rollback

Set `LostFound:AI:HybridPipeline:Enabled` back to `false`. No data migration, no schema change, no code change — the legacy path was never modified.

---

## 2. Benchmark Report

| Metric | Source | Status |
|---|---|---|
| Full solution build | `dotnet build` (whole Forge solution) | **Executed. 0 errors, 0 warnings.** |
| Automated tests | `dotnet test` on `LostFound.Application.Tests` | **Executed. 58/58 passing** (including this Part's 15 new tests across `SemanticSearchOrchestratorTests`, `ObjectTypeCompatibilityServiceTests`, `InMemorySearchAnalyticsRecorderTests`, `SearchQualityMetricsCalculatorTests`, `AiMatchingServiceHybridDispatchTests`). One run hit a transient native-SQLite race under parallel xUnit collections (pre-existing `KnowledgeSqliteConnectionFactory` infra from Phase 2A Part 3, unrelated to this Part's changes) that did not reproduce on immediate re-run. |
| End-to-end search latency under real production traffic | `ISearchAnalyticsRecorder.GetSnapshot().AverageLatencyMilliseconds` / `.P95LatencyMilliseconds` | **Not measured.** This workspace has no LocalDB/real report corpus to search against at production scale — the recorder itself is real and unit-tested (`InMemorySearchAnalyticsRecorderTests`), but nothing here has generated representative traffic for it to aggregate. |
| Precision@K / Recall@K / MAP / NDCG / MRR | `ISearchQualityMetricsCalculator` | **Formulas verified against known examples. Not run against real search results** — no labeled relevance dataset exists (see §3). |

---

## 3. Search Quality Validation (prerequisite, still not done)

`SearchQualityMetricsCalculator` implements correct, standard IR formulas but has never been run against real data, because no labeled relevance dataset exists anywhere in this project. Building even a small set (tens to low hundreds of `(query, expected-relevant-report-IDs)` pairs, hand-labeled from real or realistic synthetic lost-and-found scenarios, covering Arabic/English/Urdu and mixed-language queries) is the single most important remaining action before any quantitative search-quality claim can be made about either the legacy or the new hybrid pipeline.

---

## 4. Operational Runbook

### 4.1 Health checks

- `IAiPlatformDiagnostics` (Phase 2A Part 5) — embedding runtime, Knowledge Store, Embedding Store health.
- `ISearchAnalyticsRecorder.GetSnapshot()` (this Part) — search volume (total/hybrid/legacy split), average and P95 latency, zero-result rate, language distribution.
- Both are already registered in DI (`AddLostFoundAiDiagnostics`, `AddLostFoundProductionIntegration`); wiring either into an ASP.NET Core health-check/metrics endpoint is an integration-time decision for the real host project, which this workspace's scope (`modules/lostfound/src`) does not include.

### 4.2 Common operational scenarios

| Scenario | Response |
|---|---|
| Hybrid pipeline zero-result rate spikes after enabling | Check Knowledge Graph population (§1.3 step 3) and `RetrievalOptions`'s per-strategy enable flags — a misconfigured disabled retriever can silently starve recall. |
| `IObjectTypeCompatibilityService` returns `Unknown` for almost every pair | Expected against an under-populated ontology — it means "no data to judge," not "definitely unrelated," and `SemanticSearchOrchestrator` applies no confidence adjustment in that case. Import more concept/relationship data (Phase 2A Part 4) rather than treating this as a bug. |
| Confidence scores look miscalibrated after a `RankingOptions.FeatureWeights` change | Expected transiently — re-baseline `ISearchAnalyticsRecorder` after any weight change; "80% confidence" shifts meaning when the underlying weights change. |
| Local embedding runtime unhealthy | `IAiPlatformDiagnostics` reports it; `LocalFirstEmbeddingEngine` (Phase 2A Part 1/2) automatically falls back to the configured external provider — no user-facing outage, but investigate the model/tokenizer installation before it becomes one. |
| One retrieval strategy consistently failing | `CandidateGenerator` logs a warning per failed strategy and continues without it (Phase 2B Part 2) — search keeps working; check logs for root cause. |
| Need to roll back the whole hybrid pipeline | Set `LostFound:AI:HybridPipeline:Enabled` to `false` — see §1.4. |

### 4.3 Backup & restore, upgrades

Unchanged from Phase 2A Part 5's Operations Guide — this Part introduces no new persistent storage of its own (`ISearchAnalyticsRecorder`'s counters are in-memory and reset-on-restart by design, same as `IQueryCache`).

---

## 5. Exit Criteria Self-Assessment

Against Phase 2B Part 4's own stated exit criteria:

| Criterion | Status |
|---|---|
| Search works without external AI | **Architecturally achieved, not yet field-proven.** The hybrid pipeline's retrieval/ranking work entirely offline once the Knowledge Graph is populated; embeddings are local-first with provider fallback (Phase 2A Part 2), but no local ONNX model has actually been installed in this workspace (no route to a model host), so the "fully offline, no external call at all" path is exercised only as a graceful-fallback code path, not as the primary path in practice. |
| External providers act only as enhancements | **Achieved architecturally.** `IEmbeddingEngine`/`IClassificationEngine` (Phase 2A Part 1) make providers swappable/optional by design; `AiMatchingServiceHybridDispatchTests` and `LocalEmbeddingRuntimeTests` verify the fallback wiring itself. |
| All phases are integrated | **Achieved, feature-flagged.** Every Phase 2A/2B subsystem is wired into `AiMatchingService` for real (not dead code) behind `LostFound:AI:HybridPipeline:Enabled` (default `false`) — see §1.1. Not an unconditional cutover, by deliberate, user-approved choice. |
| The platform is production-ready | **Not yet.** Pending: a real installed embedding model, a real (non-seed) ontology import, a labeled relevance dataset (§3), and a soak period comparing the two paths under real traffic — none of which this isolated, offline, no-LocalDB workspace could complete. |
| Documentation and automated tests are complete | **Achieved.** Documentation: this guide plus every phase's report. Automated tests: **58/58 passing**, executed in this workspace via `dotnet test` (not merely written) — covering every Phase 2A/2B subsystem including this Part's dispatch logic, orchestrator, ontology service, and analytics/quality-metric math. |
