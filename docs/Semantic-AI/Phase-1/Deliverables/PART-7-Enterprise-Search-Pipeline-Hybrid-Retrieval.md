# PHASE 1 — Part 7 (Deliverable)
# Enterprise Search Pipeline & Hybrid Retrieval Design

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-7-Enterprise-Search-Pipeline-Hybrid-Retrieval.md`.
> Expands Part 4 §5.1's runtime flow into the full pipeline with failure handling, and implements Part 3 ADR-3 (hybrid retrieval + RRF) concretely. Architecture only — no production code was written.

---

## 1. End-to-End Pipeline

| Stage | Input | Output | Failure handling |
|---|---|---|---|
| 1. Input Validation | Raw text/image from `AiSearchAppService` | Validated request, or `UserFriendlyException` | **Unchanged from today** — `AiSearchAppService` already rejects empty text+image (Part 2 §2.2); no redesign needed. |
| 2. Language Detection | Raw text | `LanguageCode` (+ script) | If detection is inconclusive (e.g. very short/ambiguous query), default to a "mixed/unknown" mode that skips language-specific steps (3, 5) and relies on script-level normalization + the multilingual embedding model's own robustness — never blocks the query. |
| 3. Unicode Normalization | Raw text | NFC-normalized text | Deterministic, cannot fail. |
| 4. Language-specific Normalization | Normalized text + `LanguageCode` | Normalized text (letter-form collapsing, punctuation strip — extends today's `SearchTextProcessor`, Part 2 §2.9) | Deterministic, cannot fail. |
| 5. Spell Correction | Normalized text + `LanguageCode` | Corrected text (SymSpell, Part 3 §7) | If no dictionary loaded for the detected language, pass text through unchanged rather than blocking — spell correction is an enhancement, not a hard requirement, mirroring the existing philosophy applied to classification (Part 2 §2.1's `SearchClassification.Empty` fallback). |
| 6. Tokenization | Corrected text | Token list | Whitespace + script-aware tokenization; failure is not a meaningful concept here (worst case: an empty token list from an all-filler-word query, handled the same way `SearchTextProcessor.Process`'s empty-normalization edge case is handled today, Part 2 §2.9). |
| 7. Concept Detection | Tokens + `LanguageCode` | Set of resolved `ConceptId`s (via `IConceptResolver`, Part 5 §6) | Unresolved tokens are kept as raw lexical terms for the BM25 stage (§4) rather than discarded — an unknown word should still be searchable by exact text match even if the Knowledge Graph has no concept for it yet. |
| 8. Knowledge Graph Expansion | `ConceptId`s | Expanded `ConceptId` set (via `ISemanticExpander`, Part 5 §6) | Bounded depth/weight cutoff (Part 5 §9) prevents runaway expansion; an empty expansion (concept has no related concepts) is a valid, non-error outcome. |
| 9. Semantic Expansion | Expanded concepts | Expanded query representation (term set + concept set) | Same as above — this stage flattens KG expansion into the form the retrieval stage consumes. |
| 10. Embedding Retrieval | Expanded query text | Query embedding vector (via `IEmbeddingEngine`, Part 6) | Local-first with provider fallback (Part 6 §5.3); on total failure (both local and every provider fail), the pipeline continues with a **null query embedding**, and the dense-retrieval half of stage 11 is skipped — this is the top of the fallback ladder (§5). |
| 11. Hybrid Candidate Generation | Query embedding (nullable) + expanded terms | Bounded candidate shortlist (via `IHybridSearchEngine`, §3) | Every sub-signal (BM25, dense, fuzzy) degrades independently — see §5's ladder; the stage never fails outright, it only narrows which signals contributed. |
| 12. Ranking | Candidate shortlist | Scored, ordered candidates (via `IRankingEngine`, Part 4 §2.6) | Each `IScoreComponent` independently defaults to a zero contribution on missing input (e.g. no image embedding → `ImageScore = 0`), exactly as today's `CalculateTextScore`/`CalculateImageScore` already do (Part 2 §2.1) — this behavior is preserved, not redesigned. |
| 13. Confidence Calibration | Raw scores | Calibrated display scores (via `IConfidenceCalibrator`, Part 4 §2.7) | Deterministic, cannot fail. |
| 14. Explanation Generation | Final scores + facts | Natural-language explanation (via `IMatchExplanationService`, Part 4 §2.8) | Deterministic, local, cannot fail (no AI call — Part 2 §2.6's key strength, preserved). |
| 15. Final Results | — | `List<RankedReportResult>` | — |

This is the same 15-stage shape the spec requires, mapped one-to-one onto components already defined in Parts 4–6 — no new component is introduced in this Part that wasn't already designed; this Part's job is sequencing and failure-handling, not new abstractions.

---

## 2. Query Processing Architecture

Stages 2–9 (Language Detection through Semantic Expansion) are collectively "Query Processing" — owned by `ILanguageDetector`/`ITextNormalizer`/`ISpellCorrector` (Part 4 §2.2) and `IConceptResolver`/`ISemanticExpander` (Part 4 §2.3/Part 5 §6). Specific handling:

- **Multi-word concepts**: `IConceptResolver` attempts longest-match-first resolution over token n-grams (e.g. "driving license" resolves as one concept, not "driving" + "license" separately) before falling back to single-token resolution.
- **Mixed-language queries**: since language detection (stage 2) operates at the whole-query level today via the existing binary heuristic and, in the target design, per Part 3 §7's script/n-gram approach, a genuinely mixed-language query (e.g. "لقيت iPhone ذهبي", already a real example in today's `SynonymMap` comments, Part 2 §2.9) is handled by running normalization/spell-correction in a **best-effort, per-token language guess** rather than forcing one language for the whole string — token-level fallback, not a redesign of the detector itself.
- **Transliteration**: handled at the Concept Detection stage (7) — `IConceptResolver` checks transliteration variants (Part 3 §7's Buckwalter-derived table, migrated into the Knowledge Graph as `Aliases`/`DialectVariants`, Part 5 §2.1) alongside native-script terms.
- **Misspelling handling**: stage 5 (SymSpell) is the primary defense; `Misspellings` recorded directly on Concepts (Part 5 §2.1) is a secondary net for domain-specific typos SymSpell's generic dictionary wouldn't catch.
- **Dialect handling**: `DialectVariants` on Concepts (Part 5 §2.1), resolved at stage 7 exactly like any other synonym.

---

## 3. Hybrid Retrieval

Implements Part 3 ADR-3 concretely:

```
Expanded Query
   ├──→ BM25 Lexical Search (Bm25LexicalScorer, Part 4 §4)
   │      over: candidate description text + expanded term set from stage 9
   │      output: ranked candidate list (lexical scores)
   │
   └──→ Dense Vector Search (IVectorIndex.Search, Part 4 §2.5 / Part 6 §5)
          over: candidate text embeddings
          output: ranked candidate list (cosine-similarity scores)

              ↓ (both lists, independently ranked, NOT independently scored on a comparable scale)

        Reciprocal Rank Fusion (RRF)
        fused_score(candidate) = Σ 1 / (k + rank_in_list)  across both lists
        (k is a small constant, e.g. 60, per the standard RRF formulation — dampens the
         impact of rank-1 dominance and requires no score normalization between BM25 and
         cosine similarity, which are not on comparable scales — this is precisely why RRF,
         not raw score-fusion, was selected in Part 3 ADR-3)

              ↓

        Bounded Shortlist (top ~100-200 by fused rank)
              ↓
        [feeds Ranking stage, §1 stage 12 — where today's full attribute-scoring
         logic — object type, color, brand, tags, penalties, dynamic boosts — runs
         exactly as it does today, Part 2 §2.1, just against a pre-filtered shortlist
         instead of every candidate in the repository]
```

**Weighting strategy**: BM25 and dense retrieval contribute **equally** to RRF by default (both are rank-based, not score-based, so there is no weight to hand-tune at this stage — this is one of RRF's deliberate simplicity advantages, per Part 3 §11's trade-off analysis). Category/brand/color/location/time matching remain **ranking-stage** signals (§1 stage 12), not candidate-generation signals — they refine precision on an already-recalled shortlist rather than participating in initial recall, consistent with Part 4 §6's Service Responsibilities table (`IHybridSearchEngine` owns recall, `IRankingEngine` owns precision).

---

## 4. Ranking Engine

As designed in Part 4 §2.6: a `IRankingEngine` running an ordered list of `IScoreComponent`s, each corresponding to one of today's existing scoring signals (text similarity, image similarity, object-type/color/brand/tag matches, dynamic boosts, penalty tiers), summed into the same `CandidateScore.Total` formula that exists today (Part 2 §2.1) — **the ranking formula itself is not being redesigned in Phase 1**, only its structural packaging (God-class method → composable components) and its input (full candidate list → RRF-bounded shortlist). Reciprocal Rank Fusion is used only at the candidate-generation stage (§3); feature-based ranking (today's weighted scoring) remains the final-ranking method, per Part 3 §5's recommendation. Cross-Encoder reranking is explicitly deferred (Part 3 §5/ADR-6) — not part of this pipeline design.

---

## 5. Fallback Strategy

The spec's 8-tier ladder, mapped to concrete pipeline behavior — each tier is what happens when everything richer than it has failed or is unavailable:

| Tier | Condition | Behavior |
|---|---|---|
| 1. External AI | Classification/embedding providers reachable | Full pipeline as designed — richest signal available. |
| 2. Cached AI | Provider unreachable, but `IQueryProcessingCache` (Part 4 §2.4) has a cached embedding for this exact normalized query | Serve from cache — no external call needed, same quality as tier 1 for repeated queries. |
| 3. Local Embeddings | Neither live provider nor cache available | `IEmbeddingEngine`'s local ONNX path (Part 6) — this is the tier that **does not exist at all today** (Part 2 §1's central finding) and is Phase 2A's core deliverable. |
| 4. Knowledge Graph | Embeddings entirely unavailable (e.g. ONNX runtime failure) | Concept-based matching only: `IConceptResolver`/`ISemanticExpander` still resolve and expand the query; candidate generation falls back to matching on expanded concept/term overlap rather than vector similarity. |
| 5. Synonym Expansion | Knowledge Graph unavailable too (e.g. index failed to load) | Degrade further to the flat synonym/dialect data still held in the Knowledge Graph's SQLite durable store (Part 5 §8) if the in-memory index specifically is what failed, or to a minimal built-in seed list as the absolute floor. |
| 6. Fuzzy Matching | — | Simple edit-distance/substring matching directly against candidate description text — no concept resolution needed at all. |
| 7. BM25 | — | Pure lexical search, no expansion of any kind — exact-token-overlap ranking. |
| 8. Exact Match | Every richer tier unavailable | Raw substring/exact-text matching — the guaranteed-available floor; **search must never return zero results due to infrastructure failure when relevant text matches literally exist** — this is the one invariant carried over unchanged from Part 1 Principle 3 (Graceful Degradation: "Search must never fail"). |

**Relationship to Part 1's Local-First Strategy**: that priority order (Local KG → Local Embeddings → Local Ranking → Local Search → Cached AI → External AI) governs a **different question** — which *embedding source* to prefer when generating a vector (answered in Part 6 §5.3: local ONNX before any provider). This Part's 8-tier ladder governs which *retrieval signal* to fall back to when a whole capability (not just its source) is degraded. The two are complementary, not contradictory: within "tier 3, Local Embeddings" here, Part 6's local-first sourcing already applies by definition — there's no external provider involved once this tier is reached.

---

## 6. Caching Strategy

| Cache | What | Invalidation |
|---|---|---|
| Query normalization cache | Raw text → normalized text (per language) | Cleared on `ITextNormalizer`/`ISpellCorrector` dictionary version change (rare — a dataset update, Part 8) |
| Concept resolution cache | Normalized term (+ language) → `ConceptId`s | Cleared on Knowledge Graph dataset version change (Part 5 §8's SQLite → in-memory index rebuild already implies a fresh cache) |
| Embedding cache | Normalized/expanded query text → vector | Cleared on embedding model version change (Part 6 §6's mandatory versioning) |
| Search results cache | (Query signature) → ranked results, short TTL | Time-based expiry (e.g. seconds-to-low-minutes) **only** — result freshness matters more than for the caches above, since new reports are created continuously; not cached indefinitely the way the other three are. |
| Ranking features cache | Decoded candidate embedding vectors held resident in `IVectorIndex` rather than re-decoded from JSON per request (Part 6 §8) | Invalidated per-candidate on report update (incremental index update, Part 6 §4) |

All caches sit behind `IQueryProcessingCache` (Part 4 §2.4/§8) or an equivalent small set of cache abstractions — never a `static` field, closing Part 2 §2.8's finding. Versioning (model version, dataset version) is the primary invalidation mechanism throughout, rather than TTL-based expiry, **except** the results cache, where content freshness genuinely does need TTL-based expiry given continuous report creation.

---

## 7. Observability

- **Metrics**: per-stage latency (stages 1–15, §1) emitted as a structured timing breakdown per request — directly answers "where did the 300ms go" without reading log lines.
- **Tracing**: one trace per search request, spanning all 15 stages, correlated with the existing ABP request/correlation ID.
- **Structured logging**: replaces today's unconditional `LogInformation` field-dump (Part 2 §6's finding) with structured, leveled logging — `Debug` for per-field detail, `Information` for stage-level start/finish/degradation events (e.g. "fell back to tier 4"), `Warning`/`Error` reserved for genuine failures.
- **Pipeline timing**: stage-level timers feed the metrics above; also used to validate the &lt;300ms NFR (§8) empirically once implemented, rather than assumed.
- **Failure diagnostics**: every fallback-tier transition (§5) is logged with its trigger reason (timeout, exception, unavailable dependency) — the goal is that a degraded search is always explainable after the fact, not a silent quality drop nobody notices.

---

## 8. Performance Targets

Restated from Part 1 §"Non-Functional Requirements," with the concrete mechanism each target relies on:

| Target | Mechanism |
|---|---|
| &lt;300ms average search | RRF-bounded shortlist (§3) removes the O(N) full-candidate scan (Part 2 §6's finding); local ONNX embedding (Part 6) removes network round-trip latency from the hot path in the common case. |
| CPU optimized | int8-quantized ONNX models (Part 6 §8); ONNX Runtime's own CPU execution provider optimizations. |
| Thread-safe | ONNX Runtime session concurrency guarantees (Part 6 §3); `IVectorIndex`'s explicit concurrency design (Part 4 §2.5's flagged risk — reader-writer discipline required); all other new components are read-only after warm-up (Part 4 §2.2–§2.3). |
| Low allocations | Candidate-embedding caching in `IVectorIndex` instead of per-request JSON re-decode (Part 6 §8) is the single largest allocation reduction versus today. |
| Incremental indexing | `IVectorIndex` incremental add/update (Part 6 §4) — never a full index rebuild per report change. |
| Millions of semantic relationships | Part 5 §8's memory-mapped Knowledge Graph index, sized for graph traversal rather than linear scan. |

---

## 9. Sequence Diagram — Query Search (textual)

```
User → AiSearchAppService.SearchAsync
   AiSearchAppService → IQueryPipeline.SearchAsync
      IQueryPipeline → ILanguageDetector.Detect
      IQueryPipeline → ITextNormalizer.Normalize
      IQueryPipeline → ISpellCorrector.Correct
      IQueryPipeline → IConceptResolver.Resolve            (may hit Concept cache, §6)
      IQueryPipeline → ISemanticExpander.Expand             (may hit resolution cache, §6)
      IQueryPipeline → IEmbeddingEngine.GenerateEmbeddingAsync
         IEmbeddingEngine → [local ONNX session]  (primary)
         IEmbeddingEngine ⇢ [IItemClassificationProvider chain]  (fallback only, §5 tier 1-2)
      IQueryPipeline → IHybridSearchEngine.SearchAsync
         IHybridSearchEngine → Bm25LexicalScorer.Search
         IHybridSearchEngine → IVectorIndex.Search
         IHybridSearchEngine → [RRF fusion, in-process]
      IQueryPipeline → IRankingEngine.Score                  (over RRF shortlist)
      IQueryPipeline → IConfidenceCalibrator.Calibrate
      IQueryPipeline → IMatchExplanationService.Build
   IQueryPipeline → AiSearchAppService : List<RankedReportResult>
AiSearchAppService → User : List<AiSearchResultDto>
```

This is a direct sequencing of Part 4 §5.1 with the fallback/failure branches from §5 of this Part layered in — no new component appears here that wasn't already named in Parts 4–6.

---

## 10. Architecture Decision Records

**ADR-11 — RRF operates on rank, not on raw score, specifically to avoid a BM25-vs-cosine-similarity scale-normalization problem.**
*Alternatives considered*: weighted raw-score fusion (would require empirically tuning a BM25-weight/cosine-weight ratio with no labeled data to tune against — same objection Part 3 §11 raised against adopting Learning-to-Rank prematurely).
*Decision*: RRF for candidate fusion, reusing Part 3 ADR-3's reasoning directly.

**ADR-12 — The 8-tier fallback ladder is implemented as a single ordered chain-of-responsibility inside `IHybridSearchEngine`/`IEmbeddingEngine`, not as retry logic scattered across the pipeline.**
*Alternatives considered*: each pipeline stage independently deciding its own fallback.
*Decision*: centralize fallback-tier logic per capability (embedding fallback lives in `IEmbeddingEngine`, per Part 4 ADR-9; retrieval-signal fallback lives in `IHybridSearchEngine`) so `IQueryPipeline` itself never contains conditional "if provider X failed, do Y" logic — keeps the top-level pipeline a pure sequencer, consistent with Part 4 §2.1's design.

**ADR-13 — Results cache uses TTL-based invalidation; every other new cache uses version-based invalidation.**
*Alternatives considered*: uniform TTL everywhere (simpler, but stale-Knowledge-Graph or stale-embedding-model bugs would only surface after a TTL window rather than being categorically impossible); uniform version-based everywhere (would leave the results cache serving increasingly stale result sets as new reports are created, since "new report exists" isn't a version bump to anything the cache key tracks).
*Decision*: match invalidation strategy to what actually changes underneath each cache — content freshness (results) needs time; correctness (concept/embedding data) needs version-gating, not time-gating.

*End of Part 7 deliverable. No production code was written or modified.*
