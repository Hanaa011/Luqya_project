# PHASE 1 — Part 9 (Deliverable)
# Migration Strategy, Compatibility & Risk Assessment

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-9-Migration-Strategy-Compatibility-Risk-Assessment.md`.
> Converts Parts 2–8's findings and target design into a concrete, incremental migration plan. Architecture only — no production code was written.

---

## 1. Current State Assessment

| Component | Classification | Justification |
|---|---|---|
| `AiSearchAppService.cs` | **Keep** | Clean, correctly depends on the abstraction; only internal callee (`IAiMatchingService` → `IQueryPipeline`) changes name/shape, not this file's own logic (Part 2 §2.2). |
| `AiMatchingService.cs` | **Refactor (decompose)** | Logic is largely correct and battle-tested; the problem is structural (7 responsibilities in one class), not behavioral (Part 2 §2.1, §11). Decompose into `IQueryPipeline` + the components in Part 4 §2, preserving scoring formulas exactly. |
| `AIProviderOptions.cs` | **Keep, extend** | Add `IValidateOptions` (Part 2 §11's cheap win); no structural change. |
| `ConfidenceCalibrator.cs` | **Refactor (relocate behind interface)** | Calibration curve logic is good and explicitly preserved (Part 4 §2.7) — only its packaging changes. |
| `LostFoundAiProvidersServiceCollectionExtensions.cs` | **Refactor** | Switch-statement provider selection becomes a registry (Part 4 §2.9); behavior for existing providers is unchanged. |
| `MatchExplanationGenerator.cs` | **Refactor (restructure)** | The only component Part 2 recommended a structural rewrite for, not just relocation — binary language branching cannot extend to Urdu+ (Part 2 §2.6, Part 4 §2.8). Deterministic, local, no-AI-call behavior is preserved. |
| `ObjectTypeRelationship.cs` | **Replace** | Superseded by Knowledge Graph traversal (Part 5 §6); its *intent* (tiered mismatch penalty) is preserved, its *mechanism* (hardcoded 5-cluster table) is not. |
| `QueryProcessingCache.cs` | **Replace** | Superseded by `IQueryProcessingCache` (Part 4 §2.4/§8); static-field pattern is removed entirely. |
| `SearchTextProcessor.cs` | **Refactor (data migrated, logic partly kept)** | Normalization *algorithm* (Arabic letter-form collapsing, intent-word stripping) is sound and kept; the *data* (hardcoded `SynonymMap`) migrates into the Knowledge Graph as seed data (Part 5 §7, Part 8 §3). |
| `AI/Providers/ClassificationPromptBuilder.cs` / `ClassificationJsonParser.cs` | **Keep** | Both are sound, defensively written, and remain the shared contract every classification provider uses (Part 2 §2.10) — no redesign needed. |
| `AI/Providers/GeminiVisionHelper.cs` (retry/backoff logic) | **Keep, generalize** | This file's resiliency pattern is the *template* the new `ResilientProviderDecorator` (Part 4 §2.9) generalizes to every provider — not rewritten, extracted and reused. |
| `AI/Providers/*ClassificationProvider.cs` / `*EmbeddingProvider.cs` / `*VisionHelper.cs` (13 remaining files) | **Keep, wrap** | No provider-specific logic changes; each gets wrapped by the resilience decorator rather than modified internally. |
| `ReportMatchingBackgroundJob.cs` | **Refactor** | Orchestration logic is correct; decompose the single large `ExecuteAsync` method into named pipeline stages (Part 2 §2.11) and fix logging severity (cheap win, Part 2 §11). |
| `ReportAppService.cs` | **Keep** | No violations found (Part 2 §2.12). |
| — | **Deprecate/Remove** | **None** — no component reviewed in Part 2 warrants outright removal; every finding was structural (needs decomposition/relocation) or a genuine capability gap (needs a new component alongside, not instead of, existing ones). This directly satisfies `CLAUDE.md`'s "preserve existing functionality unless intentionally redesigned" and "prefer improving over replacing." |

---

## 2. Migration Matrix

| File | Current responsibility | Future responsibility | Migration complexity | Breaking-change risk | Dependencies affected | Required tests |
|---|---|---|---|---|---|---|
| `AiMatchingService.cs` | Full pipeline (7 responsibilities) | Thin `IQueryPipeline` sequencer | High (largest single change) | Low if scoring formulas are copied verbatim into `IScoreComponent`s (behavior-preserving refactor) | `AiSearchAppService` (interface name only) | Full regression suite comparing old vs. new scores on a fixed candidate set — **mandatory before cutover** (§5). |
| `ConfidenceCalibrator.cs` | Static calibration | `IConfidenceCalibrator` impl | Low | Very low (pure relocation) | `AiMatchingService`/`IQueryPipeline` | Unit tests: identical control points, identical output curve. |
| `ObjectTypeRelationship.cs` | Static cluster table | Knowledge Graph traversal | High (new subsystem dependency) | **Medium** — traversal-derived tiers may not numerically match the old hardcoded table for every existing type pair | `IRankingEngine`'s object-type score component | Comparison test: for every object-type pair the old table covered, verify the new traversal produces the same tier classification before cutover; document and justify any intentional differences. |
| `SearchTextProcessor.cs` | Static normalization + synonym map | `ITextNormalizer`/`IConceptResolver` | High (data migration + new subsystem) | **Medium** — synonym expansion behavior must match for every existing `SynonymMap` entry post-migration | `AiMatchingService`/`IQueryPipeline` | Comparison test: every existing `SynonymMap`/`IntentWords`/`StopWords` entry produces the same normalized output via the new path. |
| `QueryProcessingCache.cs` | Static cache | `IQueryProcessingCache` | Low-Medium | Low (cache is a performance optimization, not a correctness dependency — worst case of a cache bug is slower, not wrong, results) | `AiMatchingService`/`IQueryPipeline` | Cache-hit/miss behavior tests; no correctness-of-results tests needed beyond what already covers scoring. |
| `MatchExplanationGenerator.cs` | Static per-language builders | Template + resource `IMatchExplanationService` | Medium | Low (explanation text is display-only, not scoring — a wording change is not a "breaking" change in the correctness sense, though it is user-visible) | `AiMatchingService`/`IQueryPipeline` | Snapshot tests: existing Arabic/English explanation outputs remain textually equivalent (not necessarily byte-identical, but equivalent in meaning) for a fixed set of `ReasonSummary` inputs. |
| `AI/Providers/*` (14 files) | Direct HTTP calls, inconsistent resiliency | Same calls, wrapped in resilience decorator | Low per-file | Low (decorator is additive — wraps, doesn't alter, existing call logic) | DI registration only | Integration tests against a mock HTTP handler verifying retry/backoff triggers correctly and existing success-path behavior is unchanged. |
| `ReportMatchingBackgroundJob.cs` | Monolithic `ExecuteAsync` | Staged pipeline | Medium | Low (same external calls, same order, just extracted into named methods) | None external | Existing background-job integration test (if any) re-run unchanged; new unit tests per extracted stage. |

---

## 3. Compatibility Strategy

- **Public interfaces**: `IAiSearchAppService`/`AiSearchInputDto`/`AiSearchResultDto` (external HTTP contract) are **unchanged** — every migration above happens beneath `AiSearchAppService`, which is explicitly classified "Keep" (§1). Clients of the search API observe no contract change.
- **DTOs**: unchanged, per above.
- **Existing provider contracts**: `IItemClassificationProvider`/`IEmbeddingProvider` interfaces are unchanged; only their DI wiring (registry vs. switch statement, Part 4 §2.9) and an additive resilience wrapper change — existing provider implementations require **zero code changes**.
- **Configuration**: `LostFound:AI:*` configuration keys are unchanged; new configuration (embedding model path, Knowledge Graph dataset version, vector index settings) is additive under the same `LostFound:AI` section, not a breaking rename.
- **Dependency Injection registrations**: `AddLostFoundAiProviders(configuration)` remains the single call site (Part 2 §2.5) — its *internals* change (registry instead of switch), its *external calling convention* does not.
- **Existing API behavior**: search results, scores, and explanations should be **behaviorally equivalent or better** post-migration for existing queries — not merely "the code compiles." This is why §2's migration matrix calls out comparison/snapshot testing specifically for every component whose *mechanism* changes (Knowledge Graph traversal, synonym migration), not just components that are purely relocated.
- **Where compatibility cannot be fully preserved**: the object-type mismatch penalty tier and synonym expansion results **may** shift slightly once driven by Knowledge Graph traversal instead of the old hardcoded tables, because the graph's data is richer (more object types, more synonyms) than the ~25-type/~15-concept tables it replaces. This is an **intentional, expected quality improvement**, not a regression — but it must be measured and documented (§5), not assumed silently equivalent.

---

## 4. Rollout Plan

Eight phases, each independently verifiable, per the spec's required structure:

1. **Foundation** — `AI/Abstractions/*` interfaces created; existing static classes remain the *only* implementations initially (interfaces wrap current behavior with zero logic change, i.e. `ConfidenceCalibrator` becomes `PiecewiseLinearConfidenceCalibrator : IConfidenceCalibrator` with identical internals). Verifiable via: application builds, existing behavior is bit-for-bit identical (this phase changes packaging only, not logic).
2. **Local AI** — `IEmbeddingEngine`'s ONNX local path (Part 6) built and wired as a fallback-*first* option, but not yet the default (feature-flagged, defaulting to today's provider-only behavior). Verifiable via: local embedding output compared against provider embedding output on a sample set for sanity, without affecting production traffic yet.
3. **Knowledge Graph** — `IKnowledgeGraph`/`IConceptResolver`/`ISemanticExpander` built and populated (Part 5, Part 8), running *alongside* the old `ObjectTypeRelationship`/`SearchTextProcessor` in shadow mode (both compute their result, only the old one is used, differences are logged for comparison). Verifiable via: shadow-mode diff report showing where the two diverge and why.
4. **Hybrid Search** — `IHybridSearchEngine`/`IVectorIndex` built (Part 7 §3), running in shadow mode against the existing full-candidate-scan path. Verifiable via: shortlist-recall comparison (does the RRF shortlist contain every candidate the old full scan would have scored positively?).
5. **Provider Integration** — resilience decorator (Part 4 §2.9) applied to all providers; provider registry replaces the switch statement. Verifiable via: existing provider integration tests pass unchanged, plus new retry-behavior tests.
6. **Optimization** — quantization, batching, caching tuning (Part 6 §8, Part 7 §6) applied once the above are functionally verified — performance work deliberately sequenced *after* correctness work, not interleaved with it.
7. **Validation** — feature flags flipped from shadow-mode to live for each subsystem, one at a time (local embeddings first, then Knowledge Graph, then hybrid retrieval), each with its own before/after quality comparison (§6's KPIs) gating the next flip.
8. **Production rollout** — all feature flags default-on; old static classes (`ObjectTypeRelationship`, `SearchTextProcessor`'s hardcoded tables, `QueryProcessingCache`) are removed only **after** their replacements have run live without regression for an operationally-reasonable soak period (a specific duration is an operational decision for Phase 2B, not fixed here).

This phased, shadow-mode-then-flag-flip structure is what makes "each phase independently verifiable" concrete rather than aspirational — every phase after Foundation has an explicit comparison step before it's allowed to affect real traffic.

---

## 5. Rollback Strategy

| Component | Rollback mechanism | Data corruption risk |
|---|---|---|
| Dataset updates | Restore previous SQLite file + rebuild in-memory index (Part 8 §5) | None — SQLite import is transactional; a bad import never partially commits. |
| Embedding models | Version-tagged storage (Part 6 §6) means reverting the active model config immediately stops new embeddings from that model; **existing** vectors from the old model remain valid and usable (no re-embedding required to roll back, only to complete a forward migration) | None. |
| Search pipeline | Feature flags (§4, phase 7-8) — flipping a flag back to the old code path is instant and requires no data change, since old components (`ObjectTypeRelationship` etc.) are only *removed*, not modified, until soak-period completion | None. |
| Knowledge graph | Same as dataset updates — versioned SQLite file restore | None. |
| Provider configuration | Configuration-only change (`LostFound:AI:Provider`), already instantaneous today, unchanged by this migration | None. |

The consistent pattern across every row — **rollback never mutates or repairs data, it only restores a previous version or flips a flag** — is deliberate: it's what makes "rollback must not corrupt persisted data" true by construction rather than by careful execution.

---

## 6. Testing Strategy

- **Unit tests**: every new `IScoreComponent`, `IConfidenceCalibrator`, `ITextNormalizer`, etc. implementation gets direct unit tests — genuinely easier to write post-migration than today, since Part 2 §5 found the current code's static-class coupling makes isolated unit testing of `AiMatchingService`'s internals difficult without exercising the whole class.
- **Integration tests**: `IQueryPipeline` end-to-end tests against a fixed, known candidate-report fixture set, comparing scores/rankings before and after each rollout phase (§4).
- **Regression tests**: the comparison/snapshot tests specified per-component in §2's migration matrix — these exist specifically to catch unintended behavior drift during decomposition, not just to confirm "it still compiles."
- **Performance benchmarks**: per-stage latency (Part 7 §7's observability design) measured before/after each rollout phase against the &lt;300ms NFR (Part 1, Part 7 §8).
- **Search quality benchmarks**: requires a labeled relevance set (query → expected relevant reports) that **does not exist today** — building even a small one (tens to low hundreds of query/expected-result pairs, hand-labeled from real or synthetic lost-and-found scenarios) is a prerequisite for meaningfully validating Knowledge-Graph-driven and hybrid-retrieval changes, not an optional nicety. Flagged explicitly here because without it, "is the new pipeline actually better" can only be judged qualitatively.
- **Offline validation**: confirm the full pipeline (embedding, Knowledge Graph, ranking) functions with every external provider deliberately disabled — the direct test of Part 1's "must continue operating even when every external AI provider is unavailable" claim, which is **not true of the system today** (Part 2 §1) and must be provably true before Phase 2B is considered complete.
- **Multilingual validation**: a labeled query set specifically covering Arabic, English, Urdu, and mixed-language queries — validates the Part 3 §7 language-detection design and the Part 5 multilingual Concept model together, not each in isolation.

---

## 7. Risk Assessment

| Risk | Category | Mitigation |
|---|---|---|
| Decomposing `AiMatchingService` introduces a subtle scoring regression | Technical | §2's comparison testing is mandatory before cutover, not optional; shadow-mode rollout (§4 phases 3-4) catches divergence before it affects production. |
| Knowledge Graph traversal produces different (not necessarily worse) mismatch-penalty tiers than the old hardcoded table | Technical | Explicitly flagged as expected in §3, measured via comparison testing, and treated as a quality question to evaluate against the (new) labeled relevance set, not silently accepted or silently reverted. |
| No labeled relevance dataset exists to validate search-quality improvements | Operational | §6 flags this as a prerequisite deliverable, not deferred indefinitely. |
| Local ONNX inference has a correctness bug undetected until production | Technical | Part 6 §10's health-check-at-load, plus keeping the provider fallback path live through Phase 2A/2B (Part 6 §10) so a local-inference bug degrades to previously-working behavior, not to failure. |
| Dataset licensing issue discovered after ingestion (Arabic WordNet / Open Multilingual WordNet) | Licensing | Part 8 §7's explicit pre-ingestion sign-off gate — this risk is structurally prevented, not just mitigated after the fact. |
| Performance regression from the new abstraction layers (interface dispatch overhead, more objects) | Performance | Expected to be negligible relative to the I/O/computation costs being removed (network round-trips, full linear candidate scans) — validated empirically in rollout phase 6 (§4), not assumed. |
| Team/schedule risk: 8-phase rollout takes materially longer than a "rewrite it" approach would | Operational | Accepted deliberately — `CLAUDE.md` and Part 1 both prioritize preserving working behavior and incremental verifiability over speed of delivery; this is the stated trade-off, not an oversight. |
| Deployment risk: feature-flag infrastructure itself doesn't exist yet in this codebase | Deployment | Needs to be confirmed/built as part of Phase 2A's foundation work (it may already exist via ABP's feature management module — `Volo.Abp.FeatureManagement` — which should be checked before assuming new infrastructure is needed; flagged as an open verification item, not assumed either way). |

---

## 8. Success Metrics (KPIs)

| KPI | Target | Measured via |
|---|---|---|
| Search latency (p50/p95) | &lt;300ms average (Part 1 NFR) | Part 7 §7's per-stage timing instrumentation |
| Search quality (precision/recall against the labeled relevance set, §6) | Improve over current baseline, specific numeric target set once the baseline is first measured (no baseline exists today to set a target relative to) | Labeled relevance set evaluation, run before and after each rollout phase |
| Offline availability | 100% of pipeline stages functional with zero external providers reachable | §6's offline validation test suite |
| Memory usage | Within the deployment environment's existing operational envelope (no specific number set here — depends on actual deployment sizing, an operational input this architecture document doesn't have) | Process memory monitoring before/after each rollout phase |
| Startup time | &lt;5 seconds (Part 1 NFR) | Measured at each rollout phase, since Knowledge Graph index build and ONNX model load are the two most likely contributors to a regression here |
| CPU utilization | No regression versus current baseline under equivalent load | Load-test comparison, Phase 2A/2B implementation task |

---

## 9. File Modification Plan

Every file listed "Refactor" or "Keep, extend"/"Keep, wrap" in §1 — i.e., `AiMatchingService.cs`, `ConfidenceCalibrator.cs`, `LostFoundAiProvidersServiceCollectionExtensions.cs`, `MatchExplanationGenerator.cs`, `AIProviderOptions.cs`, all 14 files under `AI/Providers/`, `ReportMatchingBackgroundJob.cs`. None of these are deleted; all are modified in place or have their logic relocated into new files under the same existing folder (`AI/`, per Part 4 §4's folder structure), per `CLAUDE.md`'s "do not move existing files unless explicitly requested" — where logic *relocates* (e.g. `ConfidenceCalibrator`'s control points into `AI/Confidence/PiecewiseLinearConfidenceCalibrator.cs`), the original file is either reduced to a thin compatibility shim during the transition (§4 phases 1-2) or removed only after its replacement has fully soaked (§4 phase 8) — never both changed and relocated in the same step.

---

## 10. New File Creation Plan

Enumerated in full in Part 4 §4's folder structure diagram (`AI/Abstractions/*`, `AI/Language/*`, `AI/Knowledge/*`, `AI/Embeddings/*`, `AI/Search/*`, `AI/Ranking/*`, `AI/Confidence/*`, `AI/Explanation/*`, `AI/Caching/*`, `AI/Importers/*`, `AI/Diagnostics/*`) — not repeated here to avoid the two documents drifting out of sync; Part 4 §4 is the authoritative folder/file plan, this Part references it.

---

## 11. Final Migration Roadmap

```
Phase 1 (this document set)   →  Architecture only, no code            [COMPLETE upon Part 10]
Phase 2A                      →  Foundation, Local AI, Knowledge Graph,
                                  Provider Integration, Dataset Import   [rollout phases 1-3, 5]
Phase 2B                      →  Hybrid Search, Optimization, Validation,
                                  Production Rollout                     [rollout phases 4, 6-8]
```

This roadmap is deliberately identical in shape to Part 4 §10's migration alignment — Part 4 and Part 9 must agree on phase boundaries by construction, since Part 9 is the risk/rollout detail behind Part 4's structural claim.

*End of Part 9 deliverable. No production code was written or modified.*
