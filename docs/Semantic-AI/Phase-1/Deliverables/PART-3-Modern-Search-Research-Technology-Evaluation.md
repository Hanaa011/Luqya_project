# PHASE 1 — Part 3 (Deliverable)
# Modern Search Research & Technology Evaluation

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-3-Modern-Search-Research-Technology-Evaluation.md`.
> Research and architecture only. No production code was written.

**Framing constraint that shapes every recommendation below**: this is a .NET 8 / ABP Framework application (confirmed by `AI/AiSearchAppService.cs`, `Volo.Abp.*` usage throughout Part 2's review) that must run **offline-capable, CPU-first**, and currently has no separate infrastructure services deployed beyond the application itself. Recommendations favor technologies with first-class .NET interop and low operational overhead over theoretically-superior options that require standing up new distributed services, unless the trade-off is explicitly justified.

---

## 1. Enterprise Search Platforms

| Platform | Indexing | Retrieval | Ranking | Scalability | Multilingual | Verdict for this project |
|---|---|---|---|---|---|---|
| Elasticsearch / OpenSearch | Inverted index + dense vector fields (kNN) | BM25 + kNN, hybrid via `rank_features`/RRF | Script scoring, LTR plugin | Excellent, horizontal | Strong (ICU analyzers, many language analyzers) | Powerful but a **new JVM service to operate** — heavy for current scale; keep as a documented future option if report volume grows into the millions. |
| Azure AI Search | Managed inverted + vector index | BM25 + vector + hybrid (native RRF) | Semantic ranker (cross-encoder, hosted) | Managed, elastic | Good | Excellent fit **if** the platform is allowed to depend on Azure and the "offline-first" NFR is relaxed for a hosted tier — contradicts Principle 6 (Offline First) as a *primary* engine, but is a reasonable **optional cloud enhancement layer** later, consistent with Principle 8. |
| Vespa | Custom, purpose-built for hybrid+ranking | BM25 + ANN + tensor ranking | First-class ML ranking (built-in) | Excellent | Good | Best-in-class capability, steep operational complexity (its own config language, JVM+C++ hybrid runtime) — disproportionate for current team size/scale. |
| Meilisearch | Inverted index, vector search (beta, HNSW-based) | BM25-like + vector hybrid | Simple, tunable relevancy rules | Good, single-node friendly | Good, decent Arabic support | Closest in spirit to "lightweight, self-hostable, offline-friendly" — worth a **future evaluation** once report volume outgrows an in-process index, but still a separate service to run. |
| Google Search (conceptual) | N/A (not adoptable directly) | Studied for architectural ideas only: query understanding pipeline, multi-signal ranking, semantic query expansion | — | — | — | Not a candidate technology — informs the *pipeline shape* (Part 7), not a component choice. |

**Recommendation**: none of these are adopted as the primary engine in Phase 2A/2B. The current in-process, repository-backed candidate scan (Part 2, §4.4) is retained and evolved into an **in-process hybrid retrieval pipeline** (BM25-style + vector, see §4 below) rather than introducing a new search-service dependency this early. Elasticsearch/OpenSearch/Vespa/Meilisearch are documented here as the **scale-out path** once report volume genuinely requires it (see Part 9, Risk Assessment).

---

## 2. Vector Search Systems

| System | CPU performance | Memory | Persistence | Hybrid search | Offline suitability | Operational cost |
|---|---|---|---|---|---|---|
| Qdrant | Good (Rust, HNSW) | Moderate | Yes, disk-backed | Yes (native) | Good — single binary, no external deps | **New service to run and monitor** |
| Weaviate | Good | Moderate-high | Yes | Yes | Good | New service; GraphQL API adds surface area not needed here |
| FAISS | Excellent (in-process library) | Low-moderate, index-type dependent | Manual (serialize index to disk) | No (library, not a search engine — hybrid must be built around it) | Excellent — pure library, no service | **None** — it's a library, not a service |
| Milvus | Excellent at scale | High (designed for billion-scale) | Yes | Yes | Requires etcd + object storage by default — heavy for this project's scale | High — over-engineered for current needs |
| pgvector | Good at small-to-mid scale | Low (rides on existing Postgres) | Yes (it's just Postgres) | Yes, via SQL alongside full-text search | Excellent **if already on PostgreSQL** | **Zero new services if the app already uses Postgres** |

**Recommendation**: two-tier approach.

1. **Primary (Phase 2A)**: an **in-process ANN index** — either a pure-.NET HNSW implementation or a thin P/Invoke wrapper around FAISS — embedded directly in the application process. This adds zero new services, matches "offline-capable" and "single binary startup" NFRs, and is sufficient for the report volumes this platform will see for the foreseeable future (thousands to low millions of active reports, not billions).
2. **Conditional (if/when the ABP data provider is confirmed as PostgreSQL)**: `pgvector` becomes attractive as a **zero-new-infrastructure** alternative — vector search lives in the same database that already stores `Report` rows, transactionally consistent with the rest of the data. This should be confirmed against the actual EF Core provider in use before Phase 2A locks in a choice (flagged as an open question for the Part 4 architecture design).
3. **Deferred**: Qdrant/Weaviate/Milvus remain documented as the path to take **only if** report volume or query-per-second load outgrows an in-process/pgvector index — not adopted now.

---

## 3. Embedding Models

Evaluated specifically against this project's three native languages (Arabic, English, Urdu) and CPU/ONNX constraints.

| Model | Arabic | English | Urdu | Cross-lingual retrieval | Size | CPU latency | ONNX | License |
|---|---|---|---|---|---|---|---|---|
| **BGE-M3** | Strong | Strong | Supported (100+ languages) | Strong (designed for it) | ~560M params (~2.2GB fp32, ~560MB int8) | Moderate (mid-size model) | Yes (official + community exports) | MIT |
| multilingual-e5-large | Strong | Strong | Supported | Strong | ~560M | Moderate | Yes | MIT |
| multilingual-e5-base | Good | Good | Supported | Good | ~278M | Fast | Yes | MIT |
| Jina Embeddings v3 | Good | Strong | Limited/unverified for Urdu specifically | Good | ~570M | Moderate | Yes | CC-BY-NC (⚠ **not commercially free** — check current license before adoption) |
| Nomic Embed | Moderate (English-centric heritage) | Strong | Weak | Moderate | ~137M | Fast | Yes | Apache 2.0 |
| GTE (multilingual) | Good | Strong | Limited | Good | Varies (base/large) | Fast-moderate | Yes | Apache 2.0 |
| Sentence-Transformers (generic multilingual, e.g. `paraphrase-multilingual-mpnet-base-v2`) | Moderate | Good | Moderate | Moderate | ~278M | Fast | Yes | Apache 2.0 |

**Primary recommendation: BGE-M3.** It is purpose-built for multi-lingual, multi-functionality retrieval (dense + sparse + ColBERT-style multi-vector, all from one model) — which directly supports the Part 4 Hybrid Retrieval goal without needing a second sparse model, has strong published Arabic performance, includes Urdu in its training coverage, is MIT-licensed (commercially unrestricted), and has both official and community ONNX exports for CPU inference.

**Fallback recommendation: multilingual-e5-base.** Smaller/faster, same license family, well-validated multilingual retrieval quality, appropriate as a low-resource fallback (e.g. constrained deployment environments, or a faster "candidate generation" first pass before a heavier rerank).

**Explicitly not recommended for now**: Jina Embeddings v3, pending confirmation of its current commercial license terms (some Jina model versions are CC-BY-NC or require a commercial agreement) — do not adopt without a licensing sign-off (see Part 8).

---

## 4. Retrieval Strategies

| Strategy | When to use here |
|---|---|
| **BM25** (lexical/sparse) | Exact/near-exact term matches — brand names, model numbers, distinctive tokens (the existing `ContainsExactModelMention` heuristic in `AiMatchingService`, Part 2 §2.1, is a hand-rolled proxy for exactly this signal). Cheap, fast, no model dependency, works even with zero local AI infrastructure — the true "floor" of the fallback ladder. |
| **Dense retrieval** (embeddings) | Semantic/conceptual matches — synonyms, paraphrases, cross-lingual matches ("جوال" ↔ "phone"). Already partially present today via provider embeddings; becomes local once Part 6 lands. |
| **Sparse retrieval** (learned sparse, e.g. BGE-M3's sparse output) | A middle ground — term-level but weighted/expandable, catches near-misses BM25's exact tokenization would drop. |
| **Hybrid retrieval** | The target default for every query: combine BM25 + dense (+ sparse where available) rather than choosing one. |
| **Reciprocal Rank Fusion (RRF)** | The fusion method: combine ranked lists from BM25 and dense retrieval by rank position rather than raw score (raw BM25 and cosine-similarity scores are not on comparable scales — RRF sidesteps that entirely, is parameter-light, and is the de facto standard used by Elasticsearch/Azure AI Search/Vespa hybrid modes). |
| **Score Fusion** (weighted raw-score combination) | Viable alternative to RRF once enough labeled relevance data exists to tune the weights meaningfully (ties into the existing `ScoringWeights` philosophy already in the codebase) — recommended as a **second stage on top of RRF-selected candidates**, not a replacement: RRF produces a good, low-effort candidate ranking; the existing deterministic attribute-scoring layer (object type/color/brand/tags bonuses and penalties) then re-scores that candidate set exactly as it does today. |
| **Candidate Generation** | Two-stage retrieval: a cheap, high-recall stage (hybrid BM25+dense, top ~100–200) followed by the more expensive precise scoring/ranking stage (today's full `CandidateScore` computation, and later, optional cross-encoder rerank) applied only to that shortlist — this is what makes candidate-embedding-index work at scale instead of a full linear scan (directly addresses Part 2 §6's performance finding). |

**Recommendation**: adopt the two-stage **Hybrid Retrieval + RRF candidate generation → deterministic attribute re-scoring** pipeline as the Part 7 pipeline backbone. This is evolutionary, not revolutionary, relative to the current code: it inserts a cheap high-recall pre-filter ahead of the scoring logic that already exists and already works well conceptually.

---

## 5. Ranking Technologies

| Technique | Quality | Performance cost | Recommendation |
|---|---|---|---|
| Weighted scoring (current approach) | Good, hand-tunable, fully explainable | Negligible | **Keep as the primary ranking layer** — it is also what makes `MatchExplanationGenerator` possible without a second model call (Part 2 §2.6's core strength). |
| Reciprocal Rank Fusion | Good for combining heterogeneous signal lists | Negligible | Adopt for the retrieval-fusion step (§4), separate from final ranking. |
| Feature-based ranking (generalizing today's `CandidateScore` into named, swappable features) | Good, extensible | Negligible | Adopt as the refactor target for `AiMatchingService`'s scoring engine (ties directly to Part 2 §11's `IScoreComponent` recommendation). |
| Cross-Encoder reranking | Best quality — jointly encodes query+candidate rather than comparing independent embeddings | High per-pair cost — only viable on a small shortlist (e.g. top 20–50 after candidate generation), and needs a local cross-encoder model (adds another ONNX model to manage) | **Future enhancement (Phase 2B or later)**, applied only to the post-candidate-generation shortlist, not the full corpus. Not required for Phase 2A. |
| Learning-to-Rank (trained model over features) | Potentially best quality, requires labeled training data (clicks, confirmed matches) this platform does not yet systematically collect | Training/maintenance overhead | **Not recommended yet** — no relevance-labeled dataset exists to train against. Revisit once the platform has enough confirmed-match history to serve as training signal (a natural Part 9 KPI/telemetry follow-on, not a Phase 1/2 deliverable). |

**Recommendation**: production-ready strategy for Phase 2A/2B is **feature-based weighted ranking** (evolving the existing scoring engine) **+ RRF for candidate fusion**, with **cross-encoder reranking** documented as the clear, concrete next step once local ONNX inference (Part 6) is in place, and **LTR** documented as a longer-term option contingent on collecting labeled outcome data.

---

## 6. Knowledge Graph Technologies

| Source | Concept coverage | Multilingual (Ar/En/Ur) | Ontology quality | Offline usage | Import complexity | License |
|---|---|---|---|---|---|---|
| **ConceptNet** | Very broad, general-purpose common-sense relations | Arabic: good coverage; Urdu: sparse; English: excellent | Loose but rich relation types (`IsA`, `RelatedTo`, `SimilarTo`, `PartOf` — a near-direct match to Part 5's proposed ontology) | Good — downloadable full assertions dump | Moderate (large flat-file/CSV assertions, needs filtering/import pipeline) | CC-BY-SA 4.0 (assertions), some CC-BY 4.0 — **attribution required**, commercial use permitted |
| **Wikidata** | Extremely broad, especially strong for **entities** (brands, products, manufacturers) | Excellent — inherently multilingual by design (language-tagged labels) | Structured, precise (property-based), less "common-sense" | Good — full or filtered dumps downloadable | Higher (large, needs targeted extraction — e.g. only brand/product/electronics-relevant subgraphs) | **CC0** — no restrictions, no attribution required |
| **Arabic WordNet** | Arabic lexical relations (synonymy, hypernymy) | Arabic only | Good, WordNet-style (`IS_A` via hypernymy) | Good | Low-moderate | Varies by distribution — must verify per source before ingestion (flagged for Part 8) |
| **Open Multilingual WordNet** | Aggregates many language wordnets, including some Urdu coverage (via IndoWordNet-derived sets) | Partial Urdu coverage, good English (Princeton WordNet), fair Arabic | Good, standard WordNet relations | Good | Moderate (per-language license/quality varies) | **Mixed per language** — Princeton WordNet itself is a permissive custom license; several component wordnets carry their own, sometimes more restrictive, terms — must be reviewed source-by-source (Part 8) |
| BabelNet | Extremely broad (merges WordNet+Wikipedia+Wiktionary+more) | Excellent | Excellent | Requires their API/dump under license | — | **Not commercially redistributable without a paid license** — evaluation only, as instructed; **not adopted**. |

**Recommendation**: combine **Wikidata** (entities: brands, product types — CC0, zero licensing friction) with **ConceptNet** (general concept relations, synonym/related-to expansion — CC-BY-SA/CC-BY, attribution required but commercially fine) as the two primary offline-imported sources, supplemented by **Arabic WordNet** for Arabic-specific lexical depth. Urdu coverage will be the thinnest of the three target languages from any of these sources — Part 8 should budget for **manually curated / dataset-augmented** Urdu concept data rather than assuming an off-the-shelf source closes that gap, and Part 5's concept model should be designed so a concept can exist with partial language coverage (e.g. Urdu name missing) without breaking.

---

## 7. NLP Components

| Component | Recommendation | Rationale |
|---|---|---|
| **Language Detection** | Fast-path on Unicode script range (Latin vs. Arabic-script, already the basis of `MatchExplanationGenerator.LooksArabic`, Part 2 §2.6) **+** a lightweight character-n-gram statistical classifier trained offline to disambiguate **Arabic vs. Urdu** within Arabic-script text | Arabic and Urdu share the Arabic script, so a Unicode-range test alone (today's approach) **cannot** distinguish them — this is a genuine, non-obvious technical risk worth calling out explicitly. Urdu-specific letters (ٹ ڈ ڑ ں ے پ چ ژ گ) give a cheap first heuristic; a small n-gram model closes the remaining ambiguity without needing a heavy library (e.g. fastText's `lid.176`, ~126MB, is unnecessary overkill for a 3-language problem). |
| **Normalization** | Extend the existing `SearchTextProcessor` Arabic letter-form normalization (Part 2 §2.9, already solid) with an equivalent Urdu normalization pass (Urdu has its own hamza/yeh/heh variant issues distinct from Arabic's) | Reuse the proven pattern rather than a new library. |
| **Lemmatization/Stemming** | English: standard Porter/Snowball stemmer (mature, pure-C# ports exist). Arabic: light rule-based stemming (Khoja-algorithm style root/affix stripping — well-documented, portable to C#, no external service). Urdu: **normalization only** for now; no stemmer — Urdu morphological resources for a from-scratch C# implementation are thin, and a low-quality stemmer risks doing more harm than good to match quality. | Matches available tooling quality per language rather than forcing parity where the resources don't exist yet. |
| **Spell Correction** | **SymSpell** | SymSpell's reference implementation is itself C#, extremely fast (order-of-magnitude faster than naive edit-distance), and works for any language given a frequency dictionary — a natural fit for a .NET codebase and for building per-language (Ar/En/Ur) dictionaries offline from the datasets selected in Part 8. |
| **Transliteration** | Rule-based transliteration table (Buckwalter-style Arabic↔Latin scheme as a base, extended with common colloquial spellings), feeding into the Knowledge Graph's per-concept alias/variant list (Part 5) | Deterministic, offline, and extends the exact pattern already proven by `SearchTextProcessor.SynonymMap`'s transliteration entries (e.g. "ايفون" → "phone") rather than introducing an ML transliteration model. |
| **Named Entity Recognition** | **Gazetteer-based lookup against the Knowledge Graph's brand/entity list**, not a statistical NER model | The domain's entity types (brands, colors, categories) are a bounded, known vocabulary once the Knowledge Graph (Part 5) exists — dictionary lookup is deterministic, instant, and needs no model, versus a statistical NER model's training/maintenance cost for marginal benefit in this constrained domain. |

---

## 8. Inference Runtime

| Runtime | CPU performance | Memory | Portability | Deployment complexity | .NET fit |
|---|---|---|---|---|---|
| **ONNX Runtime** | Excellent (native CPU optimizations, INT8 quantization support) | Low-moderate, quantization reduces further | Excellent — Windows/Linux/macOS, x64/ARM64 | Low — single NuGet package (`Microsoft.ML.OnnxRuntime`) | **First-class** — Microsoft-maintained, designed for exactly this |
| llama.cpp (embedding support) | Good | Low (GGUF quantization) | Good | Moderate — native library + P/Invoke bindings (e.g. `LLamaSharp`) needed | Good but secondary — more relevant if/when a local generative LLM (not just embeddings) is wanted |
| TorchScript | Good | Higher | Requires libtorch native binaries | High for .NET (poor native interop story vs. ONNX) | Weak fit |
| TensorRT | Excellent, but **GPU only (NVIDIA)** | GPU memory | Poor — hardware-specific | High | Contradicts CPU-first/offline-anywhere requirement; **future GPU migration path only** (Part 6 §"Future GPU Migration Plan") |

**Recommendation**: **ONNX Runtime** as the default and only runtime for Phase 2A/2B. It has the best .NET integration of any option here by a wide margin, supports every embedding model recommended in §3 (all have or support ONNX export), and its quantization tooling directly serves the CPU-latency and memory NFRs from Part 1. llama.cpp is documented as a future option specifically if the roadmap later wants a local generative model (e.g. for richer classification without any external provider) rather than for the embedding/ranking work Phase 2A/2B actually needs.

---

## 9. Dataset Strategy (Preview)

Full dataset strategy is Part 8's deliverable; the technology-evaluation-level conclusion here is:

- **Entities/brands/products**: Wikidata (CC0) — filtered extraction, not the full dump.
- **General concept relations/synonyms**: ConceptNet (CC-BY-SA/CC-BY) — filtered to English/Arabic/(sparse) Urdu subsets.
- **Arabic lexical depth**: Arabic WordNet, license to be verified per distribution before ingestion.
- **Spell-correction dictionaries**: built offline per language from the above sources plus frequency data (not a separate dataset acquisition).
- **Urdu gap**: explicitly flagged — no evaluated source here gives Urdu the same coverage as Arabic/English; Part 8 must plan for supplementary curation, not assume parity.
- **Excluded**: BabelNet (licensing), Jina Embeddings v3 (licensing, pending verification).

---

## 10. Architecture Decisions (ADR Summary)

| ADR | Decision | Alternatives considered | Rationale |
|---|---|---|---|
| ADR-1 | In-process ANN index (HNSW/FAISS) as primary vector store, not a separate vector-DB service | Qdrant, Weaviate, Milvus | Zero new infrastructure, matches offline-first + current scale; revisit if volume grows (documented escape hatch) |
| ADR-2 | BGE-M3 as primary embedding model, multilingual-e5-base as fallback | multilingual-e5-large, Jina v3, Nomic, GTE, generic Sentence-Transformers | Best Ar/En/Ur coverage + dense/sparse/multi-vector in one model + MIT license + ONNX support |
| ADR-3 | Hybrid retrieval (BM25 + dense) fused via RRF, then re-scored by the existing deterministic attribute engine | Pure dense, pure BM25, score-fusion-only | Combines exact-match strength (BM25) with semantic strength (dense) without discarding the proven, explainable attribute-scoring layer already in production |
| ADR-4 | ONNX Runtime as the sole inference runtime for Phase 2A/2B | llama.cpp, TorchScript, TensorRT | Best .NET interop, CPU-first, quantization support, covers every recommended model |
| ADR-5 | Wikidata + ConceptNet + Arabic WordNet as the Knowledge Graph's initial import sources | BabelNet, Open Multilingual WordNet as primary | Licensing (CC0/CC-BY family, all commercially safe) and best available Ar/En coverage; Urdu gap explicitly accepted and flagged, not silently ignored |
| ADR-6 | Cross-encoder reranking and Learning-to-Rank deferred past Phase 2A/2B | Adopting either now | No local inference runtime yet (ADR-4 lands first) and no labeled relevance data exists to train/validate against |

---

## 11. Trade-off Analysis

- **In-process ANN vs. dedicated vector DB**: trades away horizontal scalability and rich operational tooling (Qdrant/Weaviate dashboards, replication) for zero new infrastructure and full alignment with "offline-capable, single-process-friendly." Acceptable now; the ADR is explicitly framed as reversible once volume justifies the switch.
- **BGE-M3's larger size vs. smaller multilingual-e5-base**: trades inference latency/memory for materially better multilingual + hybrid (dense+sparse) coverage. Mitigated by recommending e5-base as a configurable fallback, preserving the existing architecture's "Replaceability" principle rather than hard-coding one model.
- **RRF vs. pure score-fusion**: RRF is simpler and needs no tuning data now, but is theoretically less precise than a well-tuned weighted fusion once labeled data exists. The two-stage design (RRF for candidate generation, existing deterministic scoring for final ranking) captures most of the benefit of both without requiring labeled data upfront.
- **Deferring cross-encoder rerank and LTR**: accepts a lower ceiling on ranking quality now in exchange for not taking on model-serving and training-data-collection complexity before the foundation (local embeddings, Knowledge Graph) exists to build on. Both are documented, not abandoned.
- **Wikidata/ConceptNet over BabelNet**: BabelNet is arguably the single richest source studied, but its licensing is incompatible with unrestricted commercial redistribution — correctness/legality was weighted above raw coverage.

---

## 12. Final Technology Selection

| Layer | Selection |
|---|---|
| Inference runtime | ONNX Runtime |
| Primary embedding model | BGE-M3 |
| Fallback embedding model | multilingual-e5-base |
| Vector index | In-process HNSW/FAISS (pgvector conditional on confirmed Postgres usage) |
| Retrieval strategy | Hybrid (BM25 + dense) fused via RRF → existing deterministic attribute scoring |
| Ranking | Feature-based weighted ranking (evolved from current `ScoringWeights`); cross-encoder rerank deferred |
| Knowledge Graph sources | Wikidata (entities) + ConceptNet (concept relations) + Arabic WordNet (Arabic lexical depth); Urdu gap flagged |
| Spell correction | SymSpell |
| Language detection | Script-range fast path + Ar/Ur n-gram disambiguation |
| Transliteration | Rule-based table, Buckwalter-derived for Arabic |
| NER | Gazetteer lookup against Knowledge Graph |

*End of Part 3 deliverable. No production code was written or modified.*
