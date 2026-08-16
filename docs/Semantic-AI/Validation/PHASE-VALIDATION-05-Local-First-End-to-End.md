# PHASE VALIDATION 05 — Full System End-to-End Test, AI Provider Consistency & Local-vs-Gemini Comparative Benchmark

## Status of Prior Work (read before anything else)

This is not the first validation cycle. The following phases have already been executed, with real fixes applied and verified against the live system. This phase must **build on** their findings, not repeat them from zero, and must **not** re-open or re-litigate anything already confirmed working:

- Phase Validation — Local Runtime Stabilization: fixed 3 root causes preventing the local ONNX/BAAI-bge-m3 runtime from ever being used (empty model registry, wrong `OutputTensorName`, SentencePiece Unigram tokenizer incompatibility). Local runtime confirmed `Healthy` and generating correct 1024-d embeddings for English/Arabic/Urdu.
- Phase Validation 04 — Semantic Quality: fixed an empty Knowledge Graph (importer built but never invoked at startup — now runs via `OnPostApplicationInitializationAsync`) and a binary `ExactMatch` scoring defect. Documented, NOT fixed (data-content gap, out of scope by rule): Brand/Category/Material/Color entity-recognition vocabulary is empty because the seed importer never populates those fields.
- Architecture audit / companion report — produced the single most important open finding for this phase:

> **`HybridPipelineOptions.Enabled` defaults to `false` and no configuration file sets it to `true`. As a result, 100% of live search traffic today is served by the legacy, pre-Phase-2B scoring path (`AiMatchingService`'s inline deterministic math), and the entire Phase 2B stack — `SemanticSearchOrchestrator`, `QueryPipeline`, `HybridSearchEngine` (11 retrieval strategies), `RankingEngine` (14 features), `ObjectTypeCompatibilityService` — is fully built, DI-registered, and previously validated in isolation, but is NOT reachable from any live HTTP request.**

This phase must treat that finding as still-open ground truth unless this session's own re-verification shows it has changed. Do not assume it has been fixed.

---

## Why This Phase Exists

Prior phases each tested one slice of the system in relative isolation. This phase answers three questions none of them fully answered:

1. **Does the complete production flow still work end-to-end, right now, with everything from all prior phases combined** — not simulated, not unit-tested, but through the real API, real database, real background jobs?
2. **When an AI provider is selected in configuration, is that selection actually honored consistently across the ENTIRE system** — report creation (classification + text embedding + image embedding), the background matching job, legacy search, and the hybrid pipeline if enabled — or does some part of the system silently keep using a different provider than the one configured?
3. **How does Local (BAAI/bge-m3, ONNX, offline) actually perform against Gemini, measured head-to-head on the same real queries and the same real reports, with real numbers** — and, where a real, verifiable, evidence-based fix can close the gap without replacing the model or fabricating data, apply it and re-measure.

Question 3 is new in this cycle and is now a primary objective alongside 1 and 2, per explicit instruction: produce a real Local-vs-Gemini comparison with match/agreement percentages, improve Local's results as far as legitimately possible so it can compete with Gemini, and present that comparison in the final report in detail, with multiple tests and real data — not a summary claim.

This validation is not a benchmark for its own sake, not a redesign, and not a repeat of Phase Validation 04's semantic-quality work. The comparative benchmark in this phase is scoped narrowly: measure Local vs. Gemini on identical inputs, identify any **verifiable, fixable** gap (not a fundamental model-capability gap), fix it with the smallest possible change, and re-measure. It is not license to swap in a different or larger local model, retrain anything, or hand-tune numbers to make Local look better than it actually performs.

---

## Mandatory Pre-Work — Full Project Review Before Any Test Is Run

Before writing or running a single test, read, in this order, everything already produced for this project. Do not skip any file on the assumption "I already know this from a summary" — summaries in this document are not a substitute for the source documents:

```
docs/Semantic-AI/README.md
docs/Semantic-AI/INDEX.md
PROJECT-OVERVIEW.md
PROJECT-GOALS.md

docs/Semantic-AI/Phase-1/PHASE-1-PART-1-Vision-Principles-Architecture-Review.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-2-Current-Architecture-Review.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-3-Modern-Search-Research-Technology-Evaluation.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-4-Enterprise-AI-Architecture-Design.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-5-Semantic-Knowledge-Graph-Ontology-Design.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-6-Local-AI-Stack-Embedding-Strategy.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-7-Enterprise-Search-Pipeline-Hybrid-Retrieval.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-8-Enterprise-Dataset-Strategy-Knowledge-Acquisition.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-9-Migration-Strategy-Compatibility-Risk-Assessment.md
docs/Semantic-AI/Phase-1/PHASE-1-PART-10-Final-Enterprise-AI-Blueprint.md
docs/Semantic-AI/Phase-1/Deliverables/*  (all files)

docs/Semantic-AI/Phase-2A/PHASE-2A-PART-1-Enterprise-AI-Foundation.md
docs/Semantic-AI/Phase-2A/PHASE-2A-PART-2-Local-AI-Runtime.md
docs/Semantic-AI/Phase-2A/PHASE-2A-PART-3-Semantic-Knowledge-Platform.md
docs/Semantic-AI/Phase-2A/PHASE-2A-PART-4-Dataset-Importers.md
docs/Semantic-AI/Phase-2A/PHASE-2A-PART-5-Infrastructure-Storage-Caching.md
docs/Semantic-AI/Phase-2A/Deliverables/PART-5-Production-Operations-Guide.md
docs/Semantic-AI/Phase-2A/Deliverables/PRE-IMPLEMENTATION-PLAN.md

docs/Semantic-AI/Phase-2B/PHASE-2B-PART-1-Query-Understanding-Semantic-Pipeline.md
docs/Semantic-AI/Phase-2B/PHASE-2B-PART-2-Hybrid-Retrieval-Engine.md
docs/Semantic-AI/Phase-2B/PHASE-2B-PART-3-Enterprise-Ranking-Engine.md
docs/Semantic-AI/Phase-2B/PHASE-2B-PART-4-Production-Integration.md
docs/Semantic-AI/Phase-2B/Deliverables/PART-4-Migration-Guide-and-Operational-Runbook.md

docs/Semantic-AI/Validation/PHASE-VALIDATION-02-BackgroundJob-Resilience.md
docs/Semantic-AI/Validation/PHASE-VALIDATION-03-Ranking-Calibration.md
docs/Semantic-AI/Validation/PHASE-VALIDATION-04-Semantic-Quality.md
docs/Semantic-AI/Validation/PHASE-VALIDATION-Local-Runtime-Stabilization.md
```

Then review the live source code, focused on every location relevant to provider selection, routing, and embedding quality, since these are this phase's central questions:

- `LostFound.Application/AI/AiMatchingService.cs` — the branch point between legacy and hybrid search
- `LostFound.Application/AI/AiProviderRegistry.cs` and every file in `AI/Providers/`
- `LostFound.Application/AI/Embeddings/LocalFirstEmbeddingEngine.cs`, `ProviderFallbackEmbeddingEngine.cs`
- `LostFound.Application/AI/Runtime/OnnxEmbeddingModel.cs`, `OnnxEmbeddingRuntime.cs`, `TokenizerLoader.cs`
- `LostFound.Application/AI/ClassificationEngine.cs`, `ResilientClassificationProvider.cs`
- `LostFound.Application/AI/Configuration/HybridPipelineOptions.cs`, `LocalAiRuntimeOptions.cs`
- `LostFound.Application/BackgroundJobs/ReportMatchingBackgroundJob.cs`
- `LostFound.Application/AI/Production/SemanticSearchOrchestrator.cs`
- `LostFound.Application/LostFoundApplicationModule.cs` (DI registration order — determines which `IEmbeddingEngine` implementation actually wins)
- `src/Forge.HttpApi.Host/appsettings.json` and `appsettings.Development.json` — the actual, current, live configuration, not a remembered one

Do not proceed to testing until you can state, from direct inspection performed in this session (not from memory of the documents above), exactly what `LostFound:AI:Provider`, `LostFound:AI:EmbeddingProvider`, `LostFound:AI:LocalRuntime:Enabled`, and `LostFound:AI:HybridPipeline:Enabled` are set to right now, in this repository, today.

---

# Primary Goals

## Goal A — The complete production pipeline still works end-to-end

```
Create Report
  |
  v
Report Persistence
  |
  v
Background Matching Job
  |
  v
AI Classification
  |
  v
Embedding Generation (provider per current config)
  |
  v
Semantic Matching
  |
  v
Match Creation
  |
  v
Notification Generation
  |
  v
User Search (legacy path, AND hybrid path if enabled)
  |
  v
Correct Ranking Result
```

This must be re-verified now, on top of all prior fixes combined, because no phase to date has run all of them together in one continuous session.

## Goal B — AI provider selection is honored consistently everywhere, for every provider an operator could realistically configure

For **each** of the following configurations, verify that the SAME provider is actually the one doing the work at every single AI-touching step — not just the step that configuration happens to name most directly:

| Config under test | Report classification | Report text embedding | Report image embedding | Search classification (legacy path) | Search query embedding (legacy path) | Search query embedding (hybrid path, if enabled) |
|---|---|---|---|---|---|---|
| **Local First** (`LocalRuntime:Enabled=true`, `EmbeddingProvider` unset or `"Gemini"` as fallback only) | External provider (Local has no classification) | **Local ONNX** | External provider (Local has no vision model) — confirm this is documented/expected, not a bug | External provider | **Local ONNX** | **Local ONNX** |
| **Gemini forced** (`Provider=Gemini`, `EmbeddingProvider=Gemini`, `LocalRuntime:Enabled=false`) | Gemini | Gemini | Gemini | Gemini | Gemini | Gemini |
| **Local runtime intentionally broken** (`LocalRuntime:Enabled=true` but point `ModelDirectory` at a non-existent path) | External configured provider | External configured provider (fallback engaged) | External configured provider | External configured provider | External configured provider (fallback engaged) | External configured provider (fallback engaged) |
| **Hybrid pipeline enabled** (`HybridPipeline:Enabled=true`), tested against BOTH Local First and Gemini-forced embedding configs above | (same as corresponding row) | (same as corresponding row) | (same as corresponding row) | N/A — hybrid path does not call classification at search time; confirm this directly, do not assume | (same as corresponding row) | (same as corresponding row) |

"Verify" means: read it directly from the diagnostics fields already required by this phase (`Provider Used`, `Embedding Source`), from application logs, and from direct provider-call network evidence (`api.openai.com` / `generativelanguage.googleapis.com` host-call check). Do not infer provider usage from response quality or timing alone.

**Defect pattern to hunt for:** a configuration change that is supposed to be global (e.g. switching `EmbeddingProvider` from `Local` to `Gemini`) but which, due to DI registration order, cached singletons, or a hardcoded provider reference, only actually takes effect for *some* of the six columns above. Confirm directly whether a config change requires an application restart to take effect, and whether that is documented anywhere, or a silent trap.

## Goal C — Real, measured Local-vs-Gemini comparison, with a genuine attempt to close any fixable gap

This is the newest and most detail-intensive part of this phase. It has three stages, in order: **measure → diagnose → improve-and-remeasure**. Do not skip to "improve" without a real measurement first, and do not report an "improvement" without a real before/after remeasurement using the same test set.

### C.1 — Build the comparison dataset

Use a single, fixed set of real reports and real queries, created through the real API, so that Local and Gemini are evaluated on **exactly the same inputs** — this is the only way a percentage-agreement or accuracy number means anything. Minimum required coverage, all prefixed `TESTDATA-P5-CMP:`:

- 10 exact/near-exact paraphrase pairs (same object, reworded)
- 10 Arabic dialect/typo/verb-form variants across at least 3 distinct object types
- 10 English synonym pairs
- 10 cross-language pairs (Arabic query vs. English report description or vice versa)
- 10 attribute-differentiated sets (same object type, different color/brand — e.g. black wallet vs. brown wallet, iPhone vs. Samsung)
- 10 clearly unrelated pairs (negative controls — must NOT match)
- 5 minimal-pair Arabic orthography cases (Taa Marbuta vs. Heh, hamza forms), each wide enough in the rest of the sentence to avoid triggering `DuplicateResolver` collapsing (a lesson directly carried forward from Validation-04 §3.4 — do not repeat that confound)

Total: at least 65 labeled cases, each with a documented **expected outcome** (which report(s), if any, a human would consider a correct match) decided and written down BEFORE running either provider, to avoid post-hoc rationalization of whichever provider happens to win a given case.

### C.2 — Run the full set through Local and through Gemini, unchanged, and measure

For every case, run the identical query against the identical candidate report set twice — once with the system configured for Local-only embeddings (Local First, no fallback engaged — confirm no fallback triggered for any of these calls), once with Gemini-only embeddings (Gemini forced) — with classification held constant (use the same classification provider for both runs, or note explicitly if classification itself is being varied, so that any measured difference is attributable to the embedding source and not conflated with a different classification result).

Record, per case, for both providers:

- Top-1 result and its confidence/score
- Full ranked list returned (or top 5, whichever the API returns) and scores
- Whether the top-1 result matches the pre-declared expected outcome (correct / incorrect / partially correct for multi-valid-answer cases)
- Raw cosine similarity of the query embedding against the expected-correct report's stored embedding, independent of the full ranking pipeline (isolates embedding quality from ranking/feature-weighting effects)
- Latency (embedding generation + total request time)

Compute and report, in aggregate and broken down by category (the 7 bullet groups in C.1):

- **Top-1 accuracy** for Local and for Gemini separately, against the pre-declared expected outcomes
- **Agreement rate**: the percentage of cases where Local's and Gemini's top-1 result are the same report
- **Correlation** between Local's and Gemini's raw similarity scores across all cases (e.g. Pearson or Spearman correlation coefficient) — a measure of whether the two embedding spaces "think alike" even when absolute scores differ
- **Mean/median confidence gap** between Local and Gemini on cases where both are correct, and separately on cases where they disagree
- **False positive rate** on the negative-control group, for each provider separately
- **Latency comparison** (mean, p50, p95) — Local is expected to win here; confirm and quantify by how much

This is real data collection, not a one-line "Local performs comparably" claim. Every number above must be backed by the per-case table it was computed from, and that per-case table must appear in the final report (see Deliverable section).

### C.3 — Diagnose any gap found, using evidence, not guesses

If Local's top-1 accuracy or agreement rate trails Gemini's by a non-trivial margin, investigate concrete, checkable causes before concluding "this is just a fundamental model-quality gap." In particular, check these specific, common, and legitimately fixable causes for BAAI/bge-m3-family models before accepting a gap as unfixable:

- **Missing retrieval instruction prefixes.** BGE-M3 and similar E5/BGE-family models are trained with, and officially documented to expect, different input framing for queries vs. passages/documents in retrieval use (commonly a `"query: "` style prefix for search queries and no prefix, or a different prefix, for the indexed documents). Directly check whether `OnnxEmbeddingModel`/`LocalFirstEmbeddingEngine` currently sends raw, unprefixed text for both queries and reports. If the model's own documentation (check the installed model's `config.json`/model card/README if bundled, or the model's known public documentation) specifies a recommended usage pattern that is not being followed, applying it is a **conformance fix to how an already-approved model is used** — not a model replacement or an architecture change — and is explicitly in-scope for this phase.
- **Pooling method correctness.** Confirm mean-pooling + L2-normalization (per the Local Runtime Stabilization report) is in fact the pooling method BAAI/bge-m3 expects, versus e.g. using the `sentence_embedding` output that the installed ONNX export already exposes (noted but intentionally left unused in the Local Runtime Stabilization report §6.5) — compare both empirically on a subset of the comparison set if time allows, and report which performs better, with numbers.
- **Text normalization asymmetry.** Confirm the same `TextNormalizer`/pipeline preprocessing is applied identically to both query text and report text before embedding — an asymmetry here (e.g. normalization applied to queries in `QueryPipeline` but not to the raw text embedded at report-creation time in `ReportMatchingBackgroundJob`) would silently hurt Local-vs-Gemini comparability if Gemini's provider-side preprocessing happens to compensate differently.
- **Embedding input text quality.** Confirm `report.BuildEmbeddingText(...)` and the search-time `classification.SearchText` construction produce comparably rich input for both providers — if classification (which both configurations may share) already produces a strong `SearchText` paragraph, verify Local's embedding call actually receives and uses it, rather than a shorter/rawer fallback string that only affects one provider path.
- **Tokenizer fidelity.** The Local Runtime Stabilization report explicitly flagged that byte-for-byte parity with the reference HuggingFace tokenizer was not cross-checked (§6.3). If time and environment allow, perform that check now; a tokenizer mismatch would understate Local's true embedding quality independent of the model itself.

Do not chase a gap into retraining, fine-tuning, or swapping in a different/larger embedding model — that is out of scope and would violate this phase's "do not replace the embedding model" rule. The improvements in scope here are strictly usage-conformance and pipeline-symmetry fixes, not model changes.

### C.4 — Apply verified, minimal fixes, and re-measure with the same C.1/C.2 methodology

For each concrete cause confirmed in C.3, apply the smallest fix that addresses it, rebuild, and **re-run the exact same C.1 dataset** through Local only (Gemini's numbers do not need to be re-run unless something on the Gemini side was also touched, which should not happen). Report a clean before/after table: accuracy, agreement rate, correlation, false-positive rate, per category, pre-fix vs. post-fix. If a fix makes things worse in some category, report that honestly too — do not discard an unfavorable result.

State explicitly, at the end, using the final measured numbers: how close Local now comes to Gemini (e.g. "Local reached X% top-1 accuracy vs. Gemini's Y% on this set, an agreement rate of Z%, after fix(es) applied" ), and which categories (if any) still show a meaningful, evidence-based, currently-unfixable-within-scope gap, with the specific reason why (e.g. "Gemini's provider-side classification produces richer brand-name normalization than the local pipeline's raw text, which is a classification-side effect, not an embedding-model effect, and out of scope for this phase's embedding-focused fixes").

---

# Validation Scope

## Report Creation

Verify (per current codebase, `ReportAppService.CreateAsync` → `ReportMatchingBackgroundJob`):

- Report API endpoint, DTO validation, entity creation, persistence
- Image handling, text storage, report type handling
- Background job enqueueing on every report creation, unconditionally

## Background Processing

Verify `ReportMatchingBackgroundJob`:

- Job execution order (classification before embedding — confirm this is still true and whether a classification-provider failure still blocks reaching the embedding step, per the open risk flagged in the Local Runtime Stabilization report §6.1)
- Retry behavior, failure handling, logging
- That this flow is entirely independent of `HybridPipelineOptions` — confirm directly rather than assuming prior findings are still accurate

## AI Classification Pipeline

Verify `ClassificationEngine`, provider selection, required attributes (`Category`, `ObjectType`, `Color`, `Brand`, `Tags`), and fallback-to-empty behavior on failure (per Validation-02, must still hold).

## Embedding Pipeline

Verify `LocalFirstEmbeddingEngine`, `OnnxEmbeddingRuntime`, `EmbeddingModelManager`, tokenizer loading, vector generation, and — critically — that the **same** engine instance/config is what both the background job and the search path resolve, for every configuration in the Goal B table. This section directly feeds Goal C's C.3/C.4 diagnosis work.

## Search Pipeline — BOTH paths

- **Legacy path** (`AiMatchingService`'s inline scoring): re-verify it still works correctly with all fixes from Validation 03/04 applied together.
- **Hybrid path** (`SemanticSearchOrchestrator` → `QueryPipeline` → `HybridSearchEngine` → `RankingEngine`): exercise this path live, end-to-end, through the real HTTP API. Set `LostFound:AI:HybridPipeline:Enabled = true` for the relevant test runs (a configuration change within this phase's explicit rules — not an architecture change, since the component already exists, is DI-registered, and was validated in isolation by Phase 2B). Run the same query set against both paths and record both outputs for comparison; do not silently prefer one.

## Knowledge Graph

Confirm the seed importer still runs at startup (Validation-04 fix) and that concept/relationship counts match expectations (26 concepts / 7 relationships) before relying on any hybrid-path result that depends on it.

---

# End-to-End Test Scenarios

Execute through the real, authenticated (where required) API. Do not simulate. Do not mock. Prefix any temporary data created with `TESTDATA-P5:` (or `TESTDATA-P5-CMP:` for the Goal C comparison dataset specifically) and remove it after validation, per the same discipline as Phase Validation 04. Do not destroy existing production data.

## Scenario Set 1 — Full pipeline smoke test (run once per provider configuration in the Goal B table)

1. Create a Lost report: `فقدت مفتاح سيارة كورولا أحمر` (also run the English equivalent, `Lost red Toyota Corolla key`, as a separate report).
2. Wait for background processing; capture logs for classification result, embedding source, matching.
3. Create a matching Found report: `Found Toyota Corolla red key near parking area`.
4. Verify a `Match` entity is created with score, explanation, report IDs, timestamp.
5. Search `مفتاح سيارة كورولا أحمر` via the legacy path; record top results, scores, explanations.
6. If hybrid is enabled for this run, repeat the same search against the hybrid path and record the same fields.
7. For every step above, record which provider actually executed each AI call (per the Goal B verification method).

## Scenario Set 2 — Required test cases (run against whichever configuration is currently active; repeat for at least Local First and Gemini-forced)

- **Exact match:** `black wallet` / `black wallet`
- **Semantic similarity:** `سيشوار` / `استشوار`
- **Cross-language:** `car key` query against a `مفتاح سيارة` report
- **Attribute matching:** color (`red key`), brand (`Toyota key`), object (`vehicle key`) — record whether these still show the Brand/Category/Material vocabulary gap documented in Validation-04 §3.5/§19.1

## Scenario Set 3 — Provider-switch consistency probe (Goal B core)

1. With the application running under **Local First**, create a report and confirm (via diagnostics/logs) local embedding was used.
2. Without restarting the application, change `LostFound:AI:EmbeddingProvider` to `"Gemini"` in configuration. Determine and document whether this requires an application restart to take effect — this alone may be a finding.
3. After whatever is required for the change to take effect, create a second report and a second search, and confirm Gemini is now used for **every** applicable column in the Goal B table — not just the one most obviously tied to the changed key.
4. Repeat in the opposite direction (Gemini → Local First).
5. Explicitly check whether switching `EmbeddingProvider` affects `Provider` (classification) at all, or whether they are fully independent, as the configuration schema suggests.

## Scenario Set 4 — Failure/fallback consistency

- **Local model missing/broken:** confirm clear error/fallback (not silent failure) and that the fallback provider is then used consistently for both the background job and search.
- **External provider unreachable/rate-limited:** confirm classification degrades to empty (per Validation-02) without blocking the embedding step from reaching the (healthy) local runtime; re-test the open risk flagged in the Local Runtime Stabilization report §6.1.

## Scenario Set 5 — Local-vs-Gemini comparative benchmark (Goal C)

Execute the full C.1 → C.2 → C.3 → C.4 methodology described above. This is the largest single scenario set in this phase and its results are a first-class deliverable, not a footnote.

---

# Provider Call Verification

Monitor logs and, where feasible, network-level evidence for calls to:

```
api.openai.com
generativelanguage.googleapis.com
```

For each configuration in the Goal B table, state explicitly which of these SHOULD appear and which SHOULD NOT, then confirm the actual observed calls match that expectation exactly. A call appearing where it shouldn't, or not appearing where it should, is a Goal B defect and must be investigated to root cause.

---

# Rules

Do not:

- Redesign architecture
- Replace the embedding model, or introduce a different/larger local model to "win" the comparison
- Fine-tune or retrain any model
- Add new providers
- Change ranking weights without evidence
- Fabricate Brand/Category/Material/Color vocabulary data to make hybrid-path features "look" active, or to make Local's comparison numbers look better (this remains a documented, deliberate out-of-scope gap per Validation-04 unless a separate, explicitly-scoped follow-up phase addresses it)
- Create synthetic success metrics, cherry-pick favorable comparison cases, or omit unfavorable Local-vs-Gemini results from the report
- Hide failures, including failures or gaps that make Local look worse than Gemini

Only fix:

- Verified implementation defects
- Configuration problems (explicitly including setting `HybridPipeline:Enabled=true` for testing, provider-switch config changes for Goal B, and usage-conformance fixes to the local embedding pipeline identified in Goal C.3 — e.g. missing documented prefix conventions, pooling-output selection, preprocessing symmetry)
- Runtime integration problems

Every fix must: identify root cause → apply the smallest possible change → rebuild → execute real validation (including, for Goal C fixes, a full before/after remeasurement on the unchanged comparison dataset) → document the result honestly. If a fix would touch more than the immediate defect, stop and document it as a finding for a separate follow-up phase instead of expanding scope mid-cycle.

---

# Deliverable

Generate a single report:

```
PHASE-VALIDATION-05-Local-First-End-to-End-Report.md
```

Location:

```
C:\Users\Windows 11\Desktop\Forge\SemanticReports
```

The report must contain, at minimum:

1. Validation Summary
2. Pre-Work Confirmation (explicit statement of the four config values read live at session start)
3. Environment Configuration (all configurations actually tested, listed explicitly)
4. Goal A — Full Pipeline Verification Results (per scenario set 1)
5. Goal B — Provider Consistency Matrix, filled in with actual observed results for every cell in the Goal B table, for every configuration tested
6. Hybrid Pipeline Live Results — live HTTP results for this path, compared side-by-side against legacy-path results for the same queries
7. **Goal C — Local vs. Gemini Comparative Benchmark**, containing:
   - 7a. Full comparison dataset listing (all ~65+ cases, with pre-declared expected outcomes)
   - 7b. Per-case results table for both providers (top-1 result, score, correct/incorrect, raw cosine similarity, latency) — the complete table, not a sample
   - 7c. Aggregate metrics: top-1 accuracy (Local vs. Gemini), agreement rate, score correlation, false-positive rate on negative controls, latency comparison — overall and broken down by the 7 category groups from C.1
   - 7d. Diagnosis section: which of the C.3 candidate causes were checked, what was found for each (confirmed defect / confirmed not-a-defect, with evidence either way)
   - 7e. Fixes applied (if any), each with root cause, exact change, and file(s) touched
   - 7f. Before/after remeasurement table using the identical C.1 dataset, per category
   - 7g. Final, explicit verdict: how close Local now measures to Gemini, in the actual computed numbers, and an honest statement of any remaining gap and why it remains
8. Scenario Set Results (2, 3, 4) with evidence (logs, diagnostics fields, DB rows)
9. Provider Call Verification (external host-call evidence vs. expectation, per configuration)
10. Issues Found, with severity
11. Root Cause Analysis for each issue
12. Files Modified (list every file touched, including any Goal C conformance fixes)
13. Regression Test Results (full existing test suite, before/after)
14. Carried-Forward Open Risks (explicitly re-confirm status of every open risk listed in prior validation reports — Brand/Category/Material vocabulary gap, background job per-step resilience, tokenizer byte-parity with HuggingFace reference, plaintext credentials in `appsettings.json`, restart-required-for-config-change if confirmed in Scenario Set 3)
15. Final Production Readiness Assessment — answer explicitly: **is the system safe to run in production with Local First as primary, with a real fallback provider, where a provider switch behaves as one coherent, whole-system change rather than a partial one, and with Local's measured quality now documented against Gemini's?**

---

# Final Objective

At the end of this validation we must be able to answer, with direct execution evidence — not inference from prior reports:

1. **Does the Lost & Found AI platform, right now, with every fix from every prior validation phase combined, run correctly end-to-end** — and does selecting an AI provider (Local or any external provider) actually govern the entire system consistently, with no step silently left on a different provider than the one configured?
2. **How does Local actually compare to Gemini, in real, measured numbers, across a real and varied test set** — and, after any legitimate, evidence-based, in-scope conformance fixes were applied and re-measured, how close is Local able to get to Gemini's quality while remaining the same approved local model, running fully offline?