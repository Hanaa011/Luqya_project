# PHASE-2A – Part 2
# Local AI Runtime & Embedding Platform

> **Document Type:** Software Design Specification (SDS)
> **Audience:** Claude Code
> **Depends on:** Phase 2A – Part 1

---

# Purpose

Build the complete local AI runtime responsible for multilingual embeddings and offline inference.

This phase does **not** implement search or ranking. It provides the AI execution layer.

---

# Mandatory Requirements

- External AI providers become optional.
- All embeddings must be generated locally.
- CPU inference is required.
- GPU support must be extensible.
- Internet is required only for initial model download.

---

# Mandatory Open-Source Models

Evaluate and compare at least:

- BAAI BGE-M3
- multilingual-e5-large
- multilingual-e5-base
- Jina Embeddings v3
- GTE Multilingual
- Nomic Embed
- Snowflake Arctic Embed

Choose the best model using:

- Arabic quality
- English quality
- Urdu quality
- CPU speed
- Memory usage
- Embedding quality
- License
- Offline suitability

Document the decision.

---

# Runtime

Implement a runtime abstraction:

- IEmbeddingRuntime
- IEmbeddingEngine
- IEmbeddingModel
- IEmbeddingModelManager
- IEmbeddingDownloader
- IEmbeddingCache
- IEmbeddingStore
- IEmbeddingVersionManager

Provider implementations become adapters only.

---

# ONNX Runtime

Prefer ONNX Runtime.

Requirements:

- CPU optimized
- Future CUDA support
- Batch inference
- Streaming support
- CancellationToken
- Quantized model support
- Version management

If another runtime is chosen, provide technical justification.

---

# Model Manager

Responsibilities:

- Download models
- Verify checksum
- Version models
- Upgrade models
- Rollback versions
- Detect corruption
- Lazy loading
- Warm startup
- Health diagnostics

---

# Embedding Pipeline

Pipeline:

Input

↓

Normalization

↓

Tokenization

↓

Embedding Generation

↓

Post Processing

↓

Caching

↓

Persistence

↓

Return Vector

Embeddings must never be regenerated if a valid cached version exists.

---

# Cache Strategy

Implement:

- Memory Cache
- Persistent Cache
- Version-aware Cache
- Batch Cache

Support cache invalidation when models change.

---

# Storage

Persist:

- Embedding vectors
- Model metadata
- Model versions
- Checksums
- Language metadata

Storage must be abstracted behind interfaces.

---

# Offline Installation

During installation:

1. Download approved model.
2. Validate checksum.
3. Optimize if required.
4. Register locally.
5. Build cache.
6. Operate fully offline.

No cloud dependency after installation.

---

# Diagnostics

Provide:

- Model status
- Runtime status
- Cache status
- Version information
- Inference latency
- Memory usage

---

# Security

- Validate downloaded artifacts.
- Reject unsigned or corrupted models.
- Prevent arbitrary model execution.
- Restrict model directories.

---

# Deliverables

Claude Code must implement:

- Local runtime
- Model manager
- Downloader
- Version manager
- Embedding engine
- Embedding cache
- Embedding storage
- Runtime diagnostics
- Dependency injection registration
- Documentation

Phase 2B will consume these services to build semantic retrieval, hybrid search and ranking.
