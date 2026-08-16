# PHASE 1 — Part 8 (Deliverable)
# Enterprise Dataset Strategy & Knowledge Acquisition

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-8-Enterprise-Dataset-Strategy-Knowledge-Acquisition.md`.
> Operationalizes the sources selected in Part 3 §6 into a concrete import pipeline feeding the Part 5 Knowledge Graph storage. Architecture and planning only — no production code was written.

---

## 1. Dataset Architecture

```
Raw external sources (Wikidata, ConceptNet, Arabic WordNet, manually curated)
   ↓  IDatasetImporter implementations (Part 4 §2.10), one per source
Validation → Cleaning → Normalization → Deduplication
   ↓
Concept Resolution (map raw source terms onto existing Concepts, or create new ones)
   ↓
Relationship Generation (typed edges per Part 5 §3's ontology)
   ↓
Embedding Generation (Part 6 §9 — concept-level embeddings via the same IEmbeddingEngine)
   ↓
Versioning (this Part §5)
   ↓
Local Storage (Part 5 §8 — SQLite durable store)
   ↓
Runtime Indexes (Part 5 §8 — memory-mapped fast-lookup index, rebuilt from SQLite)
```

Every stage is independently replaceable (one `IDatasetImporter` per source can be swapped/extended without touching the pipeline stages after it), and the entire pipeline runs **offline, ahead of deployment** — never invoked at request time, consistent with Part 5 §7 and Part 1 Principle 6.

---

## 2. Dataset Comparison Matrix

Consolidating Part 3 §6 (Knowledge Graph sources) and Part 3 §7 (NLP/spell-correction data) into one acquisition-focused view:

| Dataset | Type | Ar coverage | En coverage | Ur coverage | Size (raw) | License | Commercial use |
|---|---|---|---|---|---|---|---|
| Wikidata (filtered dump) | Entities (brands, products) | Good (labels) | Excellent | Fair (labels) | Full dump ~100GB+; filtered subset (electronics/personal-items entities) far smaller | CC0 | **Unrestricted** |
| ConceptNet (assertions) | General concept relations | Good | Excellent | Sparse | Full ~8GB; filtered subset smaller | CC-BY-SA 4.0 (majority) / CC-BY 4.0 (some) | Permitted, **attribution required** |
| Arabic WordNet | Lexical (synonymy, hypernymy) | Good | — | — | Small (~tens of MB) | Varies by distribution — **must be verified per source before ingestion** | Pending verification |
| Open Multilingual WordNet (Urdu-relevant subset) | Lexical | Fair | Excellent (via Princeton WordNet) | Partial | Small-moderate | **Mixed per component wordnet** | Pending per-source verification |
| Frequency dictionaries (for SymSpell) | Word frequency | Needed | Needed | Needed | Small | Varies by source corpus | Pending per-source verification |
| Manually curated seed (today's `SynonymMap`/`ObjectTypeRelationship`, Part 2 §2.7/§2.9) | Domain-specific | Existing | Existing | None yet | Trivial | Owned by the platform | **Unrestricted** |

---

## 3. Recommended Knowledge Sources

Unchanged from Part 3 §6/§9's conclusion, restated as the acquisition plan:

1. **Wikidata** — primary source for `Brands`/entity data (Part 5 §2.1). Ingest a **filtered subset** (entities typed as product/brand/manufacturer relevant to the taxonomy in Part 5 §4), not the full dump — full-dump ingestion is explicitly rejected as disproportionate to this project's needs.
2. **ConceptNet** — primary source for `RELATED_TO`/`SIMILAR_TO`/`IS_A` general relations and `Synonyms`. Ingest assertions filtered to English/Arabic (and the sparse Urdu subset that exists) relevant to the lost-and-found taxonomy.
3. **Arabic WordNet** — supplementary Arabic lexical depth, ingested once its specific distribution's license is confirmed (§7).
4. **Manually curated dataset** — the migration path for today's hand-written `SynonymMap`/`ObjectTypeRelationship` data (Part 2 §2.7/§2.9), plus the primary mechanism for closing the Urdu gap that no automated source fully closes (Part 3 §6's explicit finding).

**Not adopted**: BabelNet (licensing), broad Open Multilingual WordNet ingestion beyond a verified Urdu-relevant subset (mixed/unclear licensing across its component sources).

---

## 4. Dataset Import Pipeline

| Stage | Responsibility | Failure handling |
|---|---|---|
| Raw Dataset | Source file(s) as downloaded (Wikidata JSON dump subset, ConceptNet CSV assertions, Arabic WordNet's native format) | Import run fails fast if the expected raw file is missing/unreadable — this is an offline, operator-triggered process, not a request-time path, so failing loudly is correct (no user-facing degradation to worry about). |
| Validation | Schema/format sanity check per source (e.g. Wikidata JSON well-formed, ConceptNet CSV has expected columns) | Reject malformed records, log count, continue with the valid subset — one bad row must not abort an entire multi-hour import. |
| Cleaning | Strip source-specific noise (e.g. Wikidata's many irrelevant entity types filtered out before this stage even runs; ConceptNet assertions below a confidence/weight threshold dropped) | — |
| Normalization | Run the same `ITextNormalizer`/language-normalization logic (Part 4 §2.2) used at query time, so dataset-derived terms are stored in the identical normalized form queries will be matched against — this is the single most important correctness rule in the whole pipeline: **the import pipeline must reuse query-time normalization code, never reimplement a parallel copy of it**, or normalization drift between import time and query time would silently break matching. |
| Deduplication | Merge records that resolve to the same real-world concept across sources (e.g. Wikidata's "smartphone" entity and ConceptNet's "phone" node) | Handled via Concept Resolution (next stage), not blind exact-string dedup — a `Synonyms`/`Aliases` match against an existing Concept counts as the same concept, not a new one. |
| Concept Resolution | For each cleaned/normalized term, check whether it matches an existing Concept (via the same `IConceptResolver` logic queries use); if yes, merge data onto it; if no, create a new Concept | Ambiguous matches (a term plausibly matching two different existing Concepts) are flagged for manual review rather than auto-merged — an incorrect auto-merge silently corrupts the graph in a way that's hard to detect later (§8's data-quality rule). |
| Relationship Generation | Translate source-native relations (Wikidata properties, ConceptNet relation types, WordNet hypernymy) into Part 5 §3's typed edges | Unmappable/unrecognized source relation types are logged and skipped, not force-mapped to the nearest existing type (avoids silently misrepresenting the source data's actual meaning). |
| Embedding Generation | Batch-embed new/changed Concepts via `IEmbeddingEngine` (Part 6 §8's batching optimization applies directly here) | — |
| Versioning | Tag the import run with a dataset version (§5) | — |
| Local Storage | Write to SQLite (Part 5 §8) | Transactional — a failed import run does not partially corrupt the existing durable store; either the whole run's changes commit, or none do. |
| Runtime Indexes | Trigger the in-memory index rebuild (Part 5 §8/§10) | Index rebuild happens from the now-updated SQLite store, same mechanism as normal startup — no special-cased "hot reload" logic needed beyond what startup already does. |

Each stage is "independently replaceable" (per the spec's requirement) because each is a distinct method/component in the `IDatasetImporter` pipeline, not one monolithic import function — directly mirroring the Part 2 lesson about `ReportMatchingBackgroundJob.ExecuteAsync`'s current monolithic-method problem (Part 2 §2.11), applied proactively here instead of repeating it.

---

## 5. Dataset Versioning

- Every import run produces a **DatasetVersion** record: source, source-version/download-date, record counts (added/updated/skipped/rejected), and a content checksum.
- **Incremental updates**: a re-import of an updated source dataset diffs against the previous version's records (by source-native ID, e.g. Wikidata QID) rather than re-processing everything from scratch — new/changed source records flow through the pipeline; unchanged ones are skipped.
- **Rollback**: because SQLite is the durable store (Part 5 §8) and every import run is transactional (§4), rolling back means restoring the previous SQLite file/backup and rebuilding the in-memory index from it — no bespoke rollback logic needed beyond "restore the file, rebuild the index," which is also exactly the disaster-recovery story.
- **Validation**: post-import automated checks (§8) run before a new version is allowed to become the "active" version the application loads — a failed validation blocks promotion, leaving the previous version live.
- **Compatibility tracking**: a DatasetVersion also records which `IEmbeddingEngine` model/version (Part 6 §6) generated its concept embeddings — the same version-tagging discipline Part 6 mandates for report embeddings applies identically to concept embeddings, for the same reason (mixing embeddings from different model versions produces meaningless similarity).

---

## 6. Data Quality

| Rule | Check |
|---|---|
| Duplicate concepts | Post-import scan for near-duplicate Concepts (high name/synonym overlap, no shared `DatasetSource`) not caught by Concept Resolution's live dedup (§4) — flagged for manual review, not auto-merged. |
| Circular relationships | Graph cycle-detection over `IS_A`/`PART_OF` edges specifically (these are meant to be acyclic hierarchies; `RELATED_TO`/`SIMILAR_TO` are legitimately allowed to form cycles) — a detected `IS_A` cycle blocks version promotion (§5) rather than silently shipping a broken taxonomy. |
| Invalid translations | A `Translations` entry whose script doesn't match its declared `LanguageCode` (e.g. Latin-script text tagged as Arabic) is rejected at Validation (§4), not silently stored. |
| Broken references | Any relationship edge pointing to a non-existent `ConceptId` fails import validation for that record — never stored as a dangling reference. |
| Missing embeddings | Every Concept that should have an `EmbeddingReference` (per Part 5 §2.1, optional field) but doesn't after the Embedding Generation stage is logged as a data-quality warning, not a hard failure — a concept without an embedding still functions for lexical/graph-based matching (§5 tiers 4-8 of Part 7's fallback ladder), it just can't participate in dense retrieval. |
| Orphan concepts | A Concept with zero relationships and zero terms in any language (possible after a bad merge/deletion) is flagged for cleanup — it can never be resolved to or expanded from, so it's dead weight, not a correctness bug, but worth surfacing. |

---

## 7. Licensing Review

| Dataset | License | Commercial use | Attribution required | Offline redistribution | Update policy |
|---|---|---|---|---|---|
| Wikidata | CC0 | Yes, unrestricted | No | Yes — dumps are explicitly designed for offline redistribution | Wikidata publishes dated dumps regularly; re-import on a chosen cadence (§9), not continuously |
| ConceptNet | CC-BY-SA 4.0 (majority of assertions) / CC-BY 4.0 (some) | Yes | **Yes** — attribution must appear in product documentation/credits | Yes — designed for offline use | ConceptNet's assertions are relatively stable; infrequent re-import expected |
| Arabic WordNet | Varies by specific distribution | **Pending** — must be confirmed against the exact distribution chosen before any import runs | Likely, pending confirmation | Pending confirmation | Pending confirmation |
| Open Multilingual WordNet (Urdu subset) | Mixed per component wordnet | **Pending per-component verification** | Likely, pending confirmation | Pending confirmation | Pending confirmation |
| Manually curated data | Owned by the platform | Unrestricted | No | Yes | Continuous, as needed |

**Explicit sign-off gate**: no dataset marked "Pending" above may be ingested into the production Knowledge Graph until its specific license is verified and recorded here — this is a hard gate for Phase 2A's dataset-import work, not a formality. Wikidata, ConceptNet, and the manually curated dataset are cleared and unblocked today.

---

## 8. Update Strategy

- **Offline updates**: dataset re-imports are triggered manually or on a schedule (§9), never automatically pulled from the internet at application runtime — consistent with Part 1 Principle 6.
- **Scheduled imports**: a reasonable cadence (e.g. quarterly for Wikidata/ConceptNet, since the underlying real-world entities/concepts change slowly) rather than a fixed technical requirement — the actual cadence is an operational decision, not an architectural one, and can be tuned after Phase 2A ships without a design change.
- **Incremental rebuilds**: per §5, diffed against the previous version rather than full reprocessing.
- **Embedding regeneration policy**: only Concepts that are new or whose text materially changed get re-embedded on an update — mirrors Part 6 §4's "never regenerate unnecessarily" rule, applied to concept embeddings.
- **Cache invalidation**: a promoted new DatasetVersion invalidates the Concept resolution/expansion caches (Part 7 §6) — version-based, not time-based, exactly as Part 7 §6 already specifies.

---

## 9. Risk Assessment

| Risk | Category | Mitigation |
|---|---|---|
| Arabic WordNet / Open Multilingual WordNet license turns out unfavorable for the specific distribution chosen | Licensing | §7's explicit sign-off gate — no ingestion until verified; Wikidata+ConceptNet alone still deliver a workable Ar/En graph even if this source is ultimately excluded. |
| Urdu coverage remains materially thinner than Arabic/English even after import | Dataset | Explicitly accepted and planned for (Part 3 §6, Part 5 §5) — manually curated data is the deliberate backstop, not a hoped-for automated fix. |
| Bad auto-merge during Concept Resolution silently corrupts the graph | Data quality | §6's ambiguous-match-flags-for-review rule, plus post-import duplicate/orphan scans. |
| Import pipeline itself has a bug that ships bad data to production | Operational | Version-gated promotion (§5) — a new version only goes live after passing §6's automated quality checks; rollback is a file restore, not a data-repair operation. |
| Wikidata/ConceptNet full-dump size makes filtered extraction slow/unwieldy | Performance | Filtered-subset-only ingestion (§3), never full-dump processing in the running pipeline. |
| Dataset re-import introduces a model/dataset version mismatch | Model/versioning | §5's explicit model-version tagging on DatasetVersion records, cross-referenced with Part 6 §6's embedding versioning discipline. |
| Deployment risk: first production dataset import takes materially longer than expected, delaying Phase 2A | Deployment | Filtered, bounded-scope sources (§3) keep the initial import tractable; full risk quantification (time/compute estimate) belongs to Phase 2A's implementation planning, not this architecture document. |

*End of Part 8 deliverable. No production code was written or modified.*
