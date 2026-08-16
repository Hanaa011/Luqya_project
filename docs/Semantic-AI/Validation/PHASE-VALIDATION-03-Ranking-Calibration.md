# PHASE VALIDATION 03 — Ranking Calibration

## Overview

Previous validation phases confirmed:

- Local Runtime is operational.
- Background Job resilience is operational.
- Local embeddings are generated successfully.
- Report matching completes successfully even when external AI providers fail.

The remaining work is to validate the quality of semantic search results, ranking, confidence calibration, multilingual explanations, and provider fallback behavior.

No architectural redesign is allowed.

No new Semantic AI phases should be introduced.

The objective is to calibrate the production ranking engine.

---

# Runtime Observations

Current runtime validation shows several quality issues.

Examples:

- Confidence percentages are significantly lower than expected.
- Different objects receive similar confidence values.
- Match explanations are always returned in English.
- Search explanations do not follow the user's language.
- Search appears to execute directly against the local embedding engine instead of attempting external AI providers before falling back.
- The ranking quality differs noticeably from Gemini-generated rankings.

Example:

Query:

اضعت استشوار لونه اسود

Expected:

- Hair dryer should rank significantly higher than unrelated objects.
- Remote controls, juice, and car keys should receive substantially lower confidence.
- Confidence should better represent the actual semantic similarity.

Current ranking requires calibration.

---

# Validation Objectives

Validate the complete search pipeline from beginning to end.

Inspect every stage.

Never assume the cause.

Always verify.

---

# Validate Search Pipeline

Inspect:

- AiSearchAppService
- IAiMatchingService
- SemanticSearchOrchestrator
- RankingEngine
- HybridSearchEngine
- VectorRetriever
- BM25 retrieval
- Exact match scorer
- Embedding scorer
- Score normalization
- Confidence calibration
- Explanation generator
- Provider selection
- Provider fallback
- Local embedding runtime

Verify the complete execution order.

---

# Ranking Calibration

Investigate why confidence values remain very low.

Determine whether:

- score normalization is overly aggressive
- weighting is unbalanced
- BM25 influence is too small
- semantic similarity scaling is incorrect
- cosine similarity mapping is incorrect
- hybrid score normalization compresses confidence values
- confidence percentages are incorrectly transformed

Never guess.

Always verify mathematically.

The confidence score should accurately represent semantic similarity rather than simply compressing all results into a narrow range.

---

# Semantic Quality Validation

Verify that:

Highly related objects receive significantly higher scores than unrelated objects.

Example:

Query:

اضعت استشوار اسود

Expected ordering:

1. لقيت استشوار اسود
2. لقيت استشوار
3. لقيت مجفف شعر
4. لقيت سيشوار

Unrelated objects such as:

- ريموت
- عصير
- مفتاح سيارة

should receive much lower confidence.

Investigate why unrelated reports currently receive similar scores.

---

# Confidence Calibration

Calibrate confidence percentages.

The confidence values should better reflect real semantic certainty.

Example target ranges:

Nearly identical:
90–100%

Very strong semantic match:
80–90%

Strong match:
65–80%

Moderate match:
45–65%

Weak match:
20–45%

Minimal similarity:
Below 20%

These are target calibration ranges rather than fixed rules.

---

# Explanation Localization

Search explanations should automatically follow the user's language.

If the query is Arabic:

Return:

- Arabic match reasons
- Arabic explanation
- Arabic confidence wording

Example:

بدلاً من:

Matched primarily on ExactMatch...

Return:

تم العثور على تطابق اعتمادًا على:

- تطابق مباشر
- تشابه دلالي
- تشابه نصي

درجة الثقة: 92٪

---

If the query is English:

Return English explanations.

If another language supported by the multilingual embedding model is used, return explanations in that language whenever practical.

The explanation language should automatically follow the detected query language.

---

# Multilingual Validation

Validate multilingual behavior using the capabilities of:

BAAI/bge-m3

Verify semantic quality for supported languages.

Ensure multilingual retrieval remains consistent across languages.

---

# Provider Routing Validation

Inspect the runtime provider selection.

Determine whether search currently:

- immediately uses Local Embeddings
- bypasses Gemini
- bypasses OpenAI
- ignores configured provider priorities

Verify the actual execution order.

---

# Provider Fallback Validation

Report matching already performs resilient provider fallback.

Search should be validated against the same production resilience goals.

Verify whether search should operate using:

Preferred provider

↓

Secondary provider

↓

Local embedding runtime

when providers fail.

If current behavior differs, determine whether this is intentional architecture or an implementation defect.

Never redesign the architecture.

Only correct verified implementation defects.

---

# Diagnostics

Improve runtime diagnostics.

Logs should clearly indicate:

Selected provider

Fallback provider

Embedding source

Ranking strategy

Confidence calculation

Language detection

Explanation language

Final confidence

This information should be visible without enabling verbose debugging.

---

# Runtime Verification

After every modification:

- rebuild
- execute semantic search
- execute multilingual searches
- execute typo searches
- execute synonym searches
- execute provider failure scenarios
- verify ranking quality
- verify confidence calibration
- verify explanation language
- verify provider fallback behavior

Confirm:

- confidence values are calibrated
- explanations follow query language
- ranking quality improves
- unrelated objects receive significantly lower scores
- provider routing matches the intended architecture
- diagnostics clearly explain every stage

---

# Deliverables

Provide:

1. Root cause analysis
2. Ranking analysis
3. Confidence calibration analysis
4. Provider routing analysis
5. Explanation localization analysis
6. Files modified
7. Runtime verification
8. Remaining production risks

Create the engineering report:

C:\Users\Windows 11\Desktop\Forge\SemanticReports

Filename:

PHASE-VALIDATION-03-Ranking-Calibration-Report.md