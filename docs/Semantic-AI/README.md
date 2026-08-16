# Semantic AI Engine — Documentation Set

This folder documents the design and phased implementation of the Forge
LostFound module's offline-first, multilingual Semantic AI Engine. See
`../PROJECT-OVERVIEW.md` and `../PROJECT-GOALS.md` for the platform-level
context this work serves.

Read order is defined by [INDEX.md](INDEX.md). In short:

## Phase 1 — Architecture & Design (no code)

Ten parts (`Phase-1/PHASE-1-PART-1..10-*.md`) producing a Software Design
Specification: vision/principles, current-architecture review, technology
evaluation, target architecture, knowledge graph ontology, local AI/embedding
strategy, hybrid retrieval pipeline design, dataset strategy, migration
strategy, and a final blueprint. Completed deliverables live in
`Phase-1/Deliverables/`.

## Phase 2A — Foundation & Local Intelligence (implementation)

Five parts (`Phase-2A/PHASE-2A-PART-1..5-*.md`):

1. **Enterprise AI Foundation** — capability-based interfaces
   (`IEmbeddingEngine`, `IClassificationEngine`), provider decoupling, DI
   redesign. No local inference yet.
2. **Local AI Runtime** — ONNX-based local embedding runtime and model
   management.
3. **Semantic Knowledge Platform** — concept graph, resolver, ontology.
4. **Dataset Importers** — offline ingestion (Wikidata, ConceptNet, Arabic
   WordNet) into the knowledge graph.
5. **Infrastructure, Storage & Caching** — storage abstractions, caching,
   config, diagnostics. Exit gate before Phase 2B.

Progress deliverables live in `Phase-2A/Deliverables/`.

## Phase 2B — Query Understanding, Retrieval & Ranking (implementation)

Four parts (`Phase-2B/PHASE-2B-PART-1..4-*.md`): query understanding
pipeline, hybrid retrieval engine, enterprise ranking engine, and production
integration (wiring the pipeline into `AiMatchingService`/`AiSearchAppService`
with monitoring and rollout/rollback documentation).

Progress deliverables live in `Phase-2B/Deliverables/`.

## Implementation Reports

Each completed Part produces a Markdown implementation report saved to
`../../SemanticReports/`, describing what was built, the design decisions
made, and build/verification results.

## Rules

- Never skip a phase; never implement a future phase ahead of the current one.
- Phase 1 is documentation-only — no production code.
- All implementation work targets `modules/lostfound/src` only.
