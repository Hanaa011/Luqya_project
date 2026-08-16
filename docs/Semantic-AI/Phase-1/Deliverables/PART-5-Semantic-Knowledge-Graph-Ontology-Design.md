# PHASE 1 — Part 5 (Deliverable)
# Semantic Knowledge Graph & Ontology Design

> Output of `docs/Semantic-AI/Phase-1/PHASE-1-PART-5-Semantic-Knowledge-Graph-Ontology-Design.md`.
> Implements the `IKnowledgeGraph`/`IConceptResolver`/`ISemanticExpander` abstractions introduced in Part 4 §2.3, sourced from Part 3 §6's selected datasets (Wikidata, ConceptNet, Arabic WordNet). Architecture only — no production code was written.

---

## 1. Vision Recap

Today, "knowledge" is `ObjectTypeRelationship`'s 5-cluster dictionary and `SearchTextProcessor`'s ~15-concept synonym map (Part 2 §2.7, §2.9) — both hardcoded, English/Arabic-only, and invisibly coupled to `ClassificationPromptBuilder`'s exact prompt vocabulary. This Part replaces both with one queryable, data-driven, multilingual Concept graph that is the **single source of semantic truth** for the platform, exactly as Part 1 §"Vision" specifies: words are representations; the platform reasons over Concepts.

---

## 2. Concept-Centric Model

### 2.1 Concept Schema

| Field | Type | Notes |
|---|---|---|
| `ConceptId` | GUID | Stable, permanent identity — never reused, never derived from a name (names change/translate; IDs don't). |
| `CanonicalName` | string | One language-neutral internal label (e.g. `"phone"`), used for logging/debugging only — never shown to users. |
| `Synonyms` | list\<LocalizedTerm\> | Same-language alternate words (e.g. English: "mobile", "cell", "smartphone"). |
| `Aliases` | list\<LocalizedTerm\> | Informal/brand-adjacent alternate names (e.g. "iPhone" as a colloquial alias of the `phone` concept, distinct from `Apple` the brand entity — see §5's `ALIAS_OF`). |
| `Misspellings` | list\<LocalizedTerm\> | Common typos/errors, seeded from the SymSpell dictionaries (Part 3 §7) and refined by observed query logs over time. |
| `DialectVariants` | list\<LocalizedTerm\> | Regional wording, e.g. Arabic bag terms شنطة/حقيبة/جنطة (already present in today's `SynonymMap`, Part 2 §2.9 — migrated, not lost). |
| `Translations` | map\<LanguageCode, LocalizedTerm[]\> | One list per supported language (Ar/En/Ur today; extensible per Part 1 Principle 5). |
| `ParentConcepts` | list\<ConceptId\> | `IS_A` targets (§4 taxonomy). |
| `ChildConcepts` | list\<ConceptId\> | Inverse of `ParentConcepts`, materialized for fast downward traversal. |
| `RelatedConcepts` | list\<ConceptId\> | `RELATED_TO`/`SIMILAR_TO` targets — the graph-based replacement for `ObjectTypeRelationship`'s cluster table. |
| `Brands` | list\<ConceptId\> | References to Brand-type concepts commonly associated with this concept (e.g. `phone` → `Apple`, `Samsung`). |
| `Materials` | list\<string\> | e.g. "leather", "metal", "plastic" — free-text initially, promotable to their own concepts later if reasoning over materials is ever needed. |
| `TypicalColors` | list\<string\> | Informs future color-plausibility scoring (not implemented in Phase 1/2A — data model supports it now so it isn't a breaking schema change later). |
| `TypicalLocations` | list\<string\> | Same rationale — supports a future location-relevance signal, ties to `AiMatchingService`'s currently-stubbed `LocationBonus` (Part 2 §2.1). |
| `TypicalUsage` | string | Short free-text description, useful for future explanation enrichment. |
| `PopularityScore` | float | Frequency-derived weight (from dataset source frequency + observed query volume), used to break ties and to bias spell-correction/disambiguation toward common concepts. |
| `EmbeddingReference` | vector reference / nullable | Pointer to this concept's own embedding (Part 6), enabling concept-to-concept semantic similarity queries independent of any specific report. |
| `DatasetSource` | enum + source ID | Wikidata / ConceptNet / Arabic WordNet / manually curated — required for the licensing/attribution tracking Part 3 §6 and Part 8 call for. |

`LocalizedTerm = (LanguageCode, Text, Script?)` — script is tracked separately from language because Arabic and Urdu share a script (Part 3 §7's language-detection risk) but are different `LanguageCode`s; keeping script explicit lets normalization/matching logic reason about it without re-deriving it every time.

### 2.2 Words Reference Concepts, Not the Reverse

The resolution direction is always `word (+ language) → ConceptId` (via `IConceptResolver`), never the reverse as a primary operation — this mirrors `IConceptResolver`'s role in Part 4 §7 and is what allows "ضيعت جوال ذهبي" / "فقدت ايفون ذهبي" / "Lost gold iPhone" / "gold Apple phone" (Part 2 §2.9's own example) to all resolve to the same two Concept IDs (`phone`, `gold`) regardless of surface wording.

---

## 3. Ontology Design

| Relationship | Meaning | Example |
|---|---|---|
| `IS_A` | Taxonomic parent (hypernymy) | `Smartphone IS_A Phone`, `Phone IS_A Electronics` |
| `PART_OF` | Meronymy | `Camera Lens PART_OF Camera` |
| `RELATED_TO` | Loose, non-hierarchical association — the graph-native replacement for `ObjectTypeRelationship`'s "RelatedCluster" tier (Part 2 §2.7) | `Phone RELATED_TO Wallet` (both "small personal items," commonly reported together, but not the same kind of thing) |
| `SIMILAR_TO` | Near-synonymy between distinct concepts (not the same as `Synonyms`, which are same-concept alternate words) | `Backpack SIMILAR_TO Bag` |
| `BELONGS_TO_CATEGORY` | Links a concept to a taxonomy category node (§4) | `Wallet BELONGS_TO_CATEGORY "Personal Items"` |
| `HAS_BRAND` | Concept ↔ Brand entity association | `Phone HAS_BRAND Apple` |
| `HAS_COLOR` | Concept ↔ typical color | mirrors `TypicalColors`, expressed as an edge for graph-traversal consistency |
| `HAS_MATERIAL` | Concept ↔ typical material | mirrors `Materials` |
| `COMMON_LOCATION` | Concept ↔ typical loss/found location | supports future location scoring |
| `COMMON_OWNER` | Reserved for a future cross-report ownership-pattern signal (not populated in Phase 1/2A — modeled now to avoid a breaking schema change later) | — |
| `TRANSLATION_OF` | Cross-language term equivalence, distinct from `Translations` (which is a denormalized convenience list on the Concept itself for fast lookup — `TRANSLATION_OF` is the underlying graph edge it's derived from, kept for traceability back to source datasets) | — |
| `ALIAS_OF` | Informal name ↔ canonical concept, and Concept ↔ Concept where one is a colloquial stand-in for another (e.g. "iPhone" `ALIAS_OF` `Phone`, while also carrying a `HAS_BRAND Apple` edge) | — |

All relationships are **directed, typed edges** with an optional `Weight`/`Confidence` (populated from source-dataset confidence where available, e.g. ConceptNet assertion weights) and a `DatasetSource` (same rationale as §2.1). New relationship types are additive — the storage model (§8) does not enumerate relationship types in a closed schema, satisfying "Relationships must be extensible."

---

## 4. Taxonomy

Root categories, seeded from the lost-and-found domain (matches the spec's example exactly, extended with the categories implied by the existing `HuggingFaceClassificationProvider.CandidateCategories`, Part 2 §2.10, so the new taxonomy is a superset of what the current prompt-based classification already produces — no regression):

```
Electronics
    Phones
    Tablets
    Laptops
    Cameras
    Headphones / Audio

Personal Items
    Wallets
    Bags
        Backpacks
        Purses
    Keys
    Watches
    Glasses / Sunglasses
    Jewelry
    Umbrellas

Documents
    Passport
    ID Card
    Driving License

Clothing

Toys

Transportation Items
    Bicycles
    Vehicles (Cars, Motorcycles, Scooters)

Travel Equipment

Other / Uncategorized   (explicit catch-all — every current classification result must map somewhere;
                          mirrors HuggingFaceClassificationProvider's existing "Other" fallback,
                          Part 2 §2.10, so no classification result is ever unrepresentable)
```

Each taxonomy node is itself a Concept (category concepts and object concepts share the same schema, §2.1 — a category is simply a concept that other concepts point to via `BELONGS_TO_CATEGORY`), so the hierarchy is **just graph structure**, not a separate parallel system — this is what makes "unlimited growth" achievable: adding a category is adding one Concept row plus edges, never a schema or code change.

---

## 5. Multilingual Strategy

- Every Concept's `Translations` map covers Arabic, English, Urdu today (Part 1's native languages), with the map structure itself already accommodating Hindi/Turkish/Persian/Malay/French (Part 1's future languages) with zero schema change — only data.
- **All languages reference the same `ConceptId`.** A query in any supported language resolves, via `IConceptResolver`, to the same Concept graph — this is what makes cross-lingual retrieval (Part 3 §3's cross-lingual embedding requirement) and cross-lingual matching (an Arabic "found" report matching an English "lost" query) work without a translation step at query time.
- Per Part 3 §6's honest finding: Urdu coverage from Wikidata/ConceptNet/Arabic WordNet will be thinner than Arabic/English. The schema explicitly tolerates **partial language coverage** — a Concept may have zero Urdu terms initially — rather than requiring all-or-nothing population, so the platform can ship with the coverage it has and backfill Urdu over time (via manual curation or a future dataset) without a migration.
- Script ambiguity (Arabic vs. Urdu sharing the Arabic script, Part 3 §7) is handled at the **query-processing layer** (`ILanguageDetector`, Part 4 §2.2), not the Knowledge Graph — the graph itself is language-tagged and doesn't need to guess; only free-text queries need disambiguation before they can be resolved against it.

---

## 6. Semantic Expansion

```
User Words
   ↓  (ITextNormalizer + ISpellCorrector, Part 4 §2.2)
Normalized Terms
   ↓  (IConceptResolver.Resolve — exact + fuzzy match against Synonyms/Aliases/Misspellings/
       DialectVariants/Translations for the detected language)
Concepts
   ↓  (ISemanticExpander.Expand — traverse RELATED_TO/SIMILAR_TO/IS_A up to a bounded depth,
       weighted by edge Weight/Confidence so distant/low-confidence expansions contribute less)
Related Concepts
   ↓  (flatten back to a weighted term/concept set)
Expanded Semantic Query
```

This entirely replaces `ObjectTypeRelationship.Classify`'s hardcoded 4-relationship-tier logic (Same/RelatedCluster/Unknown/UnrelatedCluster, Part 2 §2.7) with a real graph traversal — the same *behavioral intent* (penalize a Phone-vs-Car mismatch more than a Phone-vs-Wallet mismatch) is preserved, but is now derived from actual relationship distance/weight in the graph instead of a hand-maintained lookup table, and works for any concept the graph knows about, not just the ~25 hardcoded object types today's table covers. No external AI call is involved anywhere in this expansion — it is pure local graph traversal, satisfying Part 1 Principle 6 (Offline First).

---

## 7. Dataset Sources (Cross-Reference to Part 3 §6)

| Source | Populates | Import approach |
|---|---|---|
| Wikidata | `Brands`, product-type entities, `HAS_BRAND` edges | Filtered extraction (electronics/personal-items/brand-relevant subgraph only, not the full dump) — full pipeline design is Part 8 |
| ConceptNet | `RELATED_TO`, `SIMILAR_TO`, `IS_A` general relations, `Synonyms` | Filtered to English/Arabic/(sparse) Urdu assertions relevant to the lost-and-found domain taxonomy |
| Arabic WordNet | Arabic `Synonyms`/`IS_A` depth | Direct import once license is verified (Part 8) |
| Manually curated | Domain-specific taxonomy nodes (§4), Urdu gap-filling, dialect variants not present in any automated source (e.g. today's hand-written `SynonymMap` entries, Part 2 §2.9, become the **seed data** for this manual tier, not thrown away) | One-time authored dataset, versioned like any other import source |

Import happens entirely offline, ahead of deployment — never at request time — per the spec's explicit instruction ("Design import pipelines rather than runtime internet access").

---

## 8. Storage Strategy

| Option | Startup speed | Memory | Lookup latency | Verdict |
|---|---|---|---|---|
| Pure JSON resources | Slow to parse at scale | High (fully materialized) | Poor without a separate index | Rejected as primary store — fine only for the small manually-curated seed tier |
| SQL Server (existing ABP data provider) | Fast (indexed queries) | Low (query-on-demand) | Good with proper indexes, but a request-time query for every lookup adds latency the current in-memory static tables (Part 2 §2.7, §2.9) don't have | Viable, but see below |
| SQLite (dedicated, embedded) | Fast | Low | Good | **Recommended for relationship/attribute storage** — offline-friendly, zero new service (consistent with Part 3 ADR-1's reasoning), easy to version/ship as a file, easy to back up/roll back (Part 9 will need exactly this) |
| Graph storage (dedicated graph DB) | N/A | N/A | Excellent for traversal | Rejected — new service, contradicts offline/zero-new-infrastructure posture; the relationship depth needed here (bounded-depth `RELATED_TO`/`IS_A` traversal) does not require a dedicated graph engine |
| Memory-mapped / serialized binary index | Fastest possible | Lowest | Fastest possible | **Recommended, layered on top of SQLite** — a compact word→ConceptId and ConceptId→edges index, memory-mapped at startup, rebuilt from SQLite whenever the dataset changes |

**Recommended hybrid**: **SQLite as the durable, versioned source of truth** for Concepts/Relationships/Translations (structured, queryable, easy to diff/backup/rollback), **with a memory-mapped/serialized fast-lookup index built from it at startup** for the hot paths (`IConceptResolver.Resolve`, `ISemanticExpander.Expand`) that must stay well under the Part 1 NFR of &lt;50ms embedding lookup / &lt;300ms total search. This mirrors exactly the two-tier pattern Part 3 §2 already chose for vector storage (durable store + in-process fast index) — one consistent storage philosophy across the whole platform rather than a different one per subsystem.

---

## 9. Query Strategy

| Query type | How it's answered |
|---|---|
| Synonym lookup | Direct hash lookup in the memory-mapped word→ConceptId index (§8), scoped to the detected language. |
| Concept lookup | Direct `ConceptId` → Concept record fetch from the in-memory index. |
| Multilingual lookup | Same as synonym lookup, but the word→ConceptId index is keyed by `(LanguageCode, Text)`, not `Text` alone — a word that's spelled identically in two languages resolves per-language, avoiding false cross-language collisions. |
| Hierarchy lookup | Bounded upward (`ParentConcepts`) or downward (`ChildConcepts`) traversal over the in-memory adjacency structure — no database round-trip once loaded. |
| Semantic expansion | Bounded-depth, weighted traversal over `RELATED_TO`/`SIMILAR_TO`/`IS_A` edges (§6), with a max-depth and min-weight cutoff to keep expansion bounded and prevent runaway fan-out on a densely-connected concept. |

All five query types are served from the **in-memory index**, not SQLite directly, at request time — SQLite is read only at startup (index build) and during dataset import (Part 8), never on the request hot path. This is the same "local, in-process, no I/O on the hot path" discipline the current codebase already applies well in `SearchTextProcessor`/`ObjectTypeRelationship` (Part 2 §2.7, §2.9 — both are in-memory static tables today; the redesign keeps that performance property while removing the hardcoding).

---

## 10. Knowledge Graph Architecture Diagram

```
                    ┌───────────────────────────────────────────┐
                    │  Offline (Part 8 Dataset Import Pipeline)  │
                    │  Wikidata / ConceptNet / Arabic WordNet /  │
                    │  Manually curated seed data                │
                    └───────────────────┬─────────────────────────┘
                                        │ import (one-time / scheduled, never at request time)
                                        ▼
                    ┌───────────────────────────────────────────┐
                    │  SQLite — durable, versioned source of      │
                    │  truth: Concepts, Relationships, Translations│
                    └───────────────────┬─────────────────────────┘
                                        │ build (at startup, or on dataset update)
                                        ▼
                    ┌───────────────────────────────────────────┐
                    │  In-memory / memory-mapped fast index:       │
                    │  (Language, Term) → ConceptId                │
                    │  ConceptId → Concept record                  │
                    │  ConceptId → adjacency (typed, weighted edges)│
                    └───────────────────┬─────────────────────────┘
                                        │ request-time reads only (no I/O)
                        ┌───────────────┴────────────────┐
                        ▼                                ▼
              IConceptResolver.Resolve          ISemanticExpander.Expand
              (word → ConceptId)                (ConceptId → related ConceptIds)
```

---

*End of Part 5 deliverable. No production code was written or modified.*
