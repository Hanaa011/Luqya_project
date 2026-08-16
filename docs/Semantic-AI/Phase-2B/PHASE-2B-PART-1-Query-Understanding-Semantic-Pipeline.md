# PHASE-2B – Part 1
# Query Understanding & Semantic Pipeline

> Software Design Specification (SDS)

## Purpose

Design the complete multilingual query understanding pipeline that transforms raw user text into a rich semantic search request.

This phase does **NOT** implement retrieval or ranking.
It prepares an optimized semantic query for Phase 2B Part 2.

---

# Objectives

The pipeline must understand intent instead of keywords.

Support:

- Arabic
- English
- Urdu

Architecture must allow adding additional languages without redesign.

---

# End-to-End Pipeline

```text
User Query
    ↓
Language Detection
    ↓
Unicode Normalization
    ↓
Language Specific Normalization
    ↓
Spell Correction
    ↓
Transliteration
    ↓
Tokenization
    ↓
Lemmatization / Morphology
    ↓
Intent Detection
    ↓
Entity Recognition
    ↓
Concept Resolution
    ↓
Knowledge Graph Expansion
    ↓
Synonym Expansion
    ↓
Semantic Query Builder
    ↓
Embedding Request
```

---

# Services

Implement dedicated services:

- IQueryPipeline
- ILanguageDetector
- ITextNormalizer
- ISpellCorrectionService
- ITransliterationService
- ITokenizer
- IMorphologyService
- IIntentDetector
- IEntityRecognizer
- IConceptResolver
- ISemanticExpander
- ISemanticQueryBuilder

Each service must have one responsibility.

---

# SearchTextProcessor

Completely redesign SearchTextProcessor.

It becomes an orchestrator only.

Business logic moves into dedicated services.

---

# Arabic Processing

Support:

- Diacritic removal
- Alef normalization
- Ya normalization
- Tatweel removal
- Hamza normalization
- Arabic punctuation
- Arabic digits

---

# English Processing

Support:

- Lower casing
- Lemmatization
- Plural normalization
- Stop words

---

# Urdu Processing

Support:

- Unicode normalization
- Character normalization
- Token normalization

---

# Spell Correction

Implement:

- Edit Distance
- Fuzzy Matching
- Keyboard proximity
- Semantic correction
- Dictionary assisted correction

Never silently replace text.
Keep correction confidence.

---

# Entity Recognition

Extract:

- Object
- Color
- Brand
- Material
- Category
- Location
- Date / Time
- Quantity

Return structured entities.

---

# Intent Detection

Recognize examples:

- Lost item
- Found item
- Search request
- General question

Design for extensibility.

---

# Semantic Expansion

Expand queries using:

- Knowledge Graph
- Concept aliases
- Synonyms
- Dialect words
- Common misspellings
- Related concepts

---

# Query Cache

Create a cache for processed queries.

Cache key must include:

- Normalized query
- Language
- Model version
- Knowledge version

---

# Diagnostics

Record:

- Processing time
- Detected language
- Corrections
- Extracted entities
- Expanded concepts

---

# Deliverables

Claude Code must implement:

- Complete query pipeline
- New SearchTextProcessor architecture
- Language services
- Entity extraction
- Intent detection
- Semantic expansion
- Query cache
- Diagnostics
- Documentation

Phase 2B Part 2 will consume the semantic query and perform hybrid retrieval.
