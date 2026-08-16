# PHASE-2B – Part 2
# Hybrid Retrieval Engine

> Software Design Specification (SDS)

## Purpose

Implement the enterprise Hybrid Retrieval Engine responsible for generating the best candidate results before ranking.

This phase MUST NOT perform final ranking.
Ranking belongs to Phase 2B Part 3.

---

# Objectives

Combine multiple retrieval strategies into one unified engine.

Search must never rely on a single technique.

Use a pluggable retrieval architecture.

---

# Retrieval Pipeline

```text
Semantic Query
      ↓
Retrieval Planner
      ↓
Parallel Retrieval
 ├─ BM25
 ├─ Dense Embeddings
 ├─ Knowledge Graph
 ├─ Exact Match
 ├─ Fuzzy Match
 ├─ Category Retrieval
 ├─ Brand Retrieval
 ├─ Color Retrieval
 ├─ Location Retrieval
 └─ Time Retrieval
      ↓
Candidate Merge
      ↓
Duplicate Removal
      ↓
Score Fusion
      ↓
Candidate Set
```

---

# Core Services

Implement:

- IHybridSearchEngine
- IRetrievalPlanner
- IRetrievalStrategy
- ICandidateGenerator
- ICandidateMerger
- IDuplicateResolver
- IFusionEngine
- IVectorRetriever
- IBM25Retriever
- IGraphRetriever
- IFuzzyRetriever
- IExactRetriever

Every strategy must be independently testable.

---

# Retrieval Strategies

Implement independent retrievers for:

- BM25 keyword retrieval
- Dense vector retrieval
- Knowledge graph traversal
- Exact identifier matching
- Alias matching
- Synonym retrieval
- Fuzzy search
- Category similarity
- Brand similarity
- Material similarity
- Color similarity
- Location similarity
- Time proximity

---

# Candidate Generation

Generate a large but relevant candidate pool.

Requirements:

- Parallel execution
- Cancellation support
- Configurable limits
- Deterministic output
- Source attribution

Each candidate must record which retrievers produced it.

---

# Fusion Engine

Support weighted score fusion.

Candidate score inputs include:

- BM25 score
- Embedding similarity
- Graph distance
- Exact match bonus
- Alias bonus
- Category score
- Brand score
- Color score
- Location score
- Time score

Fusion implementation must be replaceable.

Prepare for Reciprocal Rank Fusion (RRF) and weighted linear fusion.

---

# Duplicate Resolution

Merge duplicate candidates by:

- Report ID
- Concept ID
- Semantic equivalence

Preserve provenance from all retrieval sources.

---

# Performance

Requirements:

- Parallel retrieval
- Async throughout
- Memory efficient
- Streaming where possible
- Low allocations
- Ready for 100k+ concepts

---

# Diagnostics

Capture:

- Retriever execution time
- Candidate counts
- Fusion statistics
- Duplicate statistics
- Cache hits
- Retrieval failures

No single retriever failure should fail the search.

---

# Configuration

Allow enabling/disabling retrievers individually.

Support configurable weights and limits without recompilation.

---

# Deliverables

Claude Code must implement:

- HybridSearchEngine
- Retrieval planner
- All retriever interfaces
- Candidate generator
- Fusion engine
- Duplicate resolver
- Diagnostics
- Configuration
- Unit and integration tests
- Technical documentation

Phase 2B Part 3 will consume the candidate set and perform enterprise ranking, confidence calibration and AI fallback orchestration.
