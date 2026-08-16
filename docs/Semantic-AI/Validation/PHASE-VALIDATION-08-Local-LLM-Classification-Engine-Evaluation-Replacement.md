# PHASE VALIDATION 08 — Local LLM Classification Engine Evaluation & Replacement

## Objective

Perform a real, end-to-end evaluation of replacing the current rule-based LocalClassificationProvider with a local multilingual LLM dedicated to classification.

This is NOT a theoretical comparison.

This is NOT a code review.

This is NOT a benchmark based on documentation.

You must perform a real implementation and empirical evaluation using actual local models running on the current machine.

Your objective is to determine whether a local LLM can become the primary Local Classification Engine while preserving the existing Semantic Search and Embedding pipeline.

The evaluation must also cover image understanding. The current GeminiClassificationProvider is able to analyze an uploaded report image (not only text) and use it to help produce the classification. Any candidate local model must be evaluated on whether it can provide equivalent multimodal (vision + text) classification capability, entirely offline.

---

## Required Pre-Work — Full Project & History Review

Before doing anything else:

- Read the entire project, not only the files you assume are related to classification.
- Read every documentation phase in order, following:
  `C:\Users\Windows 11\Desktop\Forge\docs\Semantic-AI\INDEX.md`
  including Phase-1, Phase-2A, Phase-2B, and every completed Validation document.
- Read every previous engineering report already produced, located at:
  `C:\Users\Windows 11\Desktop\Forge\SemanticReports`
  Understand what was already validated, what was already fixed, what architectural decisions were already made, and what issues were already resolved in prior phases (including the Local-First classification fallback and the ontology/root-cause fixes from earlier phases).
- Do not repeat work already completed and verified in previous phases.
- Do not contradict or undo architectural decisions already validated in previous reports unless you find a verified defect, and if so, document it explicitly.
- Only after this complete review may you begin evaluating candidate models.

---

## Current System

Read the LostFound module completely before modifying anything.

Understand the entire classification pipeline including:

- ClassificationEngine
- LocalClassificationProvider
- GeminiClassificationProvider
- EntityRecognizer
- ConceptResolver
- Ontology
- SemanticQuery
- Search pipeline
- Embedding pipeline
- Ranking pipeline
- Matching pipeline

Understand every dependency before making any modification.

Do not assume anything.

---

## Candidate Models

Evaluate the following local multilingual instruction models:

1. Qwen2.5-7B-Instruct
2. Phi-4 Mini
3. Gemma 3

If newer or objectively better variants exist that are compatible with this project and hardware, document them and justify whether they should replace one of the candidates.

---

## Multimodal / Image Classification Requirements

The current production system, when using the Gemini provider, is able to accept a report image and use it as part of classification — similar to how a person would look at a photo of a lost item to determine what it is.

The Local Classification Engine must be evaluated for equivalent image-understanding capability, running entirely offline, with no calls to any cloud vision API.

For each candidate model, evaluate whether it (or a paired local vision component) can:

- Accept an uploaded report image as input, in addition to or instead of text.
- Identify the object shown in the image (ObjectType).
- Identify visual attributes directly from the image: Color, Brand (if a logo/label is visible), and distinguishing visual features.
- Combine image-derived information with any accompanying text description to produce a single, merged classification result (image + text, not image OR text).
- Handle cases where only an image is provided with little or no text, and cases where only text is provided with no image (current text-only behavior must be preserved).

Candidate approaches to evaluate:

1. Native multimodal local models (vision-language models) capable of both image and text understanding in a single model, if available and compatible with this project's hardware (for example, Qwen2.5-VL, or other current multilingual vision-language models — verify availability and licensing before assuming any specific model).
2. A dedicated local image-captioning/object-recognition component whose output (a structured description or detected attributes) is fed as additional context into the text classification model.

Document trade-offs between these two approaches: accuracy, latency, memory/VRAM footprint, complexity of integration, and multilingual (Arabic/English) label quality.

### Image Evaluation Dataset

Build a real image evaluation set covering common Lost & Found items (for example: wallets, phones, keys, chargers, bags, jewelry, documents, electronics, accessories), including:

- Clear, well-lit photos
- Low-quality or blurry photos
- Photos with multiple objects in frame
- Photos with visible brand logos/labels
- Photos with no visible brand or distinguishing marks
- Photos paired with a short text description
- Photos with no text description at all

### Image Evaluation Criteria

In addition to the text classification criteria already defined, measure for the image path:

- ObjectType accuracy from image alone
- Color accuracy from image alone
- Brand/logo detection accuracy from image alone
- Accuracy improvement (or degradation) when image and text are combined versus text-only
- Latency and memory/VRAM usage for the image path
- Failure behavior when the image is unclear, irrelevant, or unreadable (must degrade gracefully to text-only classification, never fail the report)

The final integrated solution must preserve full functionality for reports that include only text, only an image, or both — with no regression to the current text-only classification behavior.

---

## Requirements

Download the models.

Install everything required.

Configure local inference.

Ensure every model actually runs.

Do not assume success.

Fix any runtime issues until every candidate model works correctly.

If ONNX conversion is appropriate, evaluate it.

If native inference is preferable, explain why.

---

## Classification Task

The selected model will become responsible ONLY for classification.

Its responsibilities include:

- ObjectType
- Category
- Brand
- Color
- Tags
- SearchText

The model must NOT perform:

- Embedding generation
- Semantic search
- Ranking
- Similarity
- Matching

Those responsibilities must remain exactly as they are today.

The existing BGE-M3 ONNX embedding engine must remain untouched.

The search pipeline must continue using the current embedding model.

---

## Evaluation Dataset

Create a large multilingual evaluation dataset.

Include at minimum:

- Arabic
- English
- Mixed Arabic/English

Include many object types:

Electronics

Personal Items

Documents

Jewelry

Accessories

Bags

Keys

Remote Controls

Chargers

Headphones

Wallets

Phones

Laptops

Tablets

Passports

ID Cards

Mouse

Keyboard

Watch

Camera

Power Bank

USB Drive

Pen

Ring

Glasses

and additional realistic Lost & Found objects.

Include different writing styles.

Include spelling mistakes.

Include dialect Arabic.

Include short descriptions.

Include long descriptions.

Include noisy descriptions.

Include multiple objects inside one sentence.

Include "container vs contained item" cases.

Include intent words.

Include difficult edge cases.

---

## Evaluation Criteria

Measure:

Classification accuracy

Object extraction accuracy

Category accuracy

Brand extraction

Color extraction

Confidence

Latency

Memory usage

CPU usage

GPU usage (if applicable)

Arabic understanding

English understanding

Mixed language understanding

Robustness

Hallucination rate

Repeatability

Deterministic behavior

Structured JSON consistency

---

## Prompt Engineering

Design the optimal prompt for every candidate.

The model must always return structured JSON.

Never allow free-form responses.

Evaluate prompt robustness.

Improve prompts until maximum accuracy is achieved.

---

## Integration

Integrate ONLY the best-performing model.

Replace the current rule-based LocalClassificationProvider.

Do NOT modify the Gemini provider.

Do NOT modify the embedding engine.

Do NOT modify semantic search.

Do NOT modify ranking.

Do NOT modify matching.

Only replace the local classification implementation.

---

## Validation

Run real end-to-end validation.

Create real reports.

Store them.

Allow background jobs to execute.

Validate classification.

Validate search.

Validate matching.

Validate ranking.

Ensure the entire system still works.

Fix any issues discovered.

Repeat until stable.

---

## Regression Testing

Verify that all previous functionality still works.

Verify no regression exists.

Verify previous validation scenarios.

Verify Arabic classification.

Verify English classification.

Verify mixed-language classification.

Verify search quality.

Verify ranking quality.

---

## Deliverables

Produce a comprehensive technical report.

The report must include:

1. Architecture review
2. Model comparison
3. Installation process
4. Runtime configuration
5. Hardware utilization
6. Performance benchmarks
7. Classification benchmarks
8. Accuracy comparison
9. Latency comparison
10. Memory comparison
11. Failure analysis
12. Prompt design
13. Integration details
14. Code changes
15. Problems encountered
16. Fixes applied
17. Regression analysis
18. Final recommendation
19. Justification for selecting the final model
20. Remaining limitations
21. Image/multimodal classification evaluation results, including accuracy, latency, and memory/VRAM usage for the image path, and comparison of image+text versus text-only accuracy
22. Final recommendation on whether to adopt a native multimodal model or a paired vision-component + text-model architecture, with justification

Include real logs.

Include benchmark tables.

Include measured values.

Include evidence.

Do not estimate.

Do not speculate.

Do not claim results without executing them.

The final recommendation must be based entirely on empirical measurements collected from the running application.

---

## Report Location

Save the completed report as:

```
PHASE-VALIDATION-08-Local-LLM-Classification-Engine-Evaluation-Report.md
```

to:

```
C:\Users\Windows 11\Desktop\Forge\SemanticReports
```