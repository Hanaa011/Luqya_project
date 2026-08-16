# PHASE VALIDATION — Local Runtime Stabilization

## Project Location

Before making any changes, inspect the existing implementation across the entire solution.

Understand the complete architecture before modifying any code.

Do not assume that the issue is limited to a single file.

Trace the complete runtime flow from Dependency Injection through model discovery, runtime initialization, embedding generation, retrieval, and report matching.

Project Root:

C:\Users\Windows 11\Desktop\Forge

HTTP Host:

C:\Users\Windows 11\Desktop\Forge\src\Forge.HttpApi.Host

Lost & Found Module:

C:\Users\Windows 11\Desktop\Forge\modules\lostfound\src

All validation work must be performed against this project.

The runtime issue is expected to be somewhere inside the LostFound module and/or the Forge.HttpApi.Host project.

Use the existing implementation already present in the project.

Do not create a new implementation outside this solution.

## Overview

All planned implementation phases have already been completed.

This validation phase exists to verify that the Enterprise Semantic AI platform works correctly in a real production runtime.

This is **NOT** a new implementation phase.

This phase introduces **no new architecture**, **no redesign**, and **no new features**.

Its purpose is only to validate, stabilize, debug, test, and fix runtime or integration issues discovered after all implementation phases have been completed.

---

# Current Project Status

The following implementation phases are already complete.

## Phase 1

- Part 1
- Part 2
- Part 3
- Part 4
- Part 5
- Part 6
- Part 7
- Part 8
- Part 9
- Part 10

## Phase 2A

- Part 1
- Part 2
- Part 3
- Part 4
- Part 5

## Phase 2B

- Part 1
- Part 2
- Part 3
- Part 4

Assume every previous phase has already been implemented.

Do NOT redesign previous work.

Do NOT replace existing implementations unless a verified defect exists.

Only investigate runtime behavior and fix verified implementation issues.

---

# Manual Local Model Installation

The Local AI Runtime model has already been installed manually.

I manually downloaded the official BAAI/bge-m3 ONNX model from the Hugging Face ONNX directory.

The model files were copied manually from:

https://huggingface.co/BAAI/bge-m3/tree/main/onnx

into the project's AI-Models/Embeddings directory.

I manually copied all required model files into the project.

The project's automatic model installation mechanism was **NOT** used.

Current model location:

C:\Users\Windows 11\Desktop\Forge\src\Forge.HttpApi.Host\AI-Models\Embeddings

This directory already contains the required files, including:

- model.onnx
- model.onnx_data
- sentencepiece.bpe.model
- tokenizer.json
- tokenizer_config.json
- config.json
- special_tokens_map.json

Treat this installation as complete.

Do NOT download another model.

Do NOT reinstall the model.

Do NOT overwrite or replace these files.

Instead, verify that the existing manually installed model is correctly detected, registered as the active model, loaded by the ONNX Runtime, and used for local embedding generation.

---

# Manual Configuration Already Completed

The following configuration changes have already been completed manually.

These changes are intentional.

Do NOT revert them.

## Local Runtime Enabled

```csharp
public bool Enabled { get; set; } = true;
```

---

## Hybrid Pipeline Enabled

```csharp
public class HybridPipelineOptions
{
    public bool Enabled { get; set; } = true;
}
```

---

## Vector Retrieval Enabled

```csharp
context.Services.PostConfigure<RetrievalOptions>(options =>
{
    options.EnabledStrategies["Vector"] = true;
});
```

---

## appsettings.json

The LocalRuntime configuration has already been added.

The runtime configuration should be treated as complete.

Do NOT remove or replace it unless an actual configuration error is found.

---

# Existing Runtime Resources

The following runtime resources already exist:

- AI-Models/Embeddings
- AI-Data/embeddings.db
- AI-Data/knowledge.db

Do not recreate them.

Verify that they are being used correctly.

---

# Current Runtime Problem

Although the Local Runtime has been configured and the ONNX model has been manually installed, the system still appears to fall back to the external embedding provider.

Current runtime logs still contain messages similar to:

- OpenAI GenerateEmbeddingAsync
- HTTP 429 Too Many Requests
- Vector retrieval failed to generate a query embedding
- LocalFirstEmbeddingEngine falling back to the external provider

This indicates that the Local Runtime is not yet fully taking over embedding generation.

Determine the exact root cause before making any code changes.

Never guess.

Always verify.

---

# Validation Objectives

Perform a complete validation of the Enterprise Semantic AI runtime.

Inspect every component involved in local embedding generation.

---

# Validate Configuration

Verify:

- LocalRuntime option binding
- appsettings.json
- Options validation
- ModelDirectory
- DatabasePath
- Tensor names
- Embedding dimensions
- Maximum sequence length

Confirm that all configuration values are correctly bound at runtime.

---

# Validate Model Discovery

Verify that the runtime correctly discovers the manually installed model.

Confirm:

- Model directory exists
- Files exist
- File names match configuration
- Install path is valid
- Active model can be resolved

---

# Validate Model Loading

Verify:

- model.onnx
- model.onnx_data
- tokenizer
- tokenizer compatibility
- model version
- active model
- runtime initialization

Confirm that the model loads successfully without falling back.

---

# Validate ONNX Runtime

Inspect:

- OnnxEmbeddingRuntime
- OnnxEmbeddingModel

Verify:

- Session creation
- Tensor names
- Tokenizer loading
- Output tensors
- Embedding dimensions
- Runtime health

---

# Validate Dependency Injection

Verify registrations for:

- IEmbeddingEngine
- IEmbeddingRuntime
- IEmbeddingModelManager
- LocalFirstEmbeddingEngine
- ProviderFallbackEmbeddingEngine

Ensure the correct implementation is resolved by Dependency Injection.

---

# Validate Runtime Selection

Determine exactly why:

- _runtime.IsAvailable == false

or

why LocalFirstEmbeddingEngine falls back to the external provider.

If fallback occurs:

- identify the exact condition
- identify the exact exception
- identify the exact root cause

Do not hide the problem.

Fix the problem.

---

# Validate Retrieval

Inspect:

- RetrievalOptions
- Hybrid pipeline
- VectorRetriever
- Embedding generation
- Embedding cache
- Embedding storage
- Similarity search

Confirm that vector retrieval uses locally generated embeddings.

---

# Validate Background Jobs

Validate the complete ReportMatchingBackgroundJob execution.

Ensure that:

- Local embeddings are generated.
- Embeddings are stored.
- Retrieval uses local vectors.
- Matching executes correctly.
- Search never crashes.

---

# Validate Diagnostics

Improve diagnostics where appropriate.

Logs should clearly report:

- Runtime health
- Active model
- Model version
- Model path
- Tokenizer loading
- ONNX loading
- Selected embedding engine
- Fallback reason

Avoid silent failures.

---

# Runtime Testing

After every fix:

- rebuild the solution
- run the application
- test local embedding generation
- test report matching
- test retrieval
- test hybrid search

Continue until the runtime works correctly.

---

# Success Criteria

This validation phase is complete only when:

✓ Local ONNX model loads successfully.

✓ Runtime health reports Healthy.

✓ LocalFirstEmbeddingEngine generates embeddings locally.

✓ OpenAI is no longer used for embedding generation while the local runtime is available.

✓ Vector retrieval uses local embeddings.

✓ Embeddings are stored successfully.

✓ Search works correctly.

✓ Report matching works correctly.

✓ Runtime diagnostics confirm successful local execution.

---

# Deliverables

At the end of this validation phase provide:

1. Root cause analysis.

2. Files modified.

3. Why the runtime was falling back.

4. Verification that the local ONNX runtime is now generating embeddings.

5. Verification that OpenAI is no longer used for embeddings when the local runtime is available.

6. Any remaining production risks.

---

# Engineering Rules

Do NOT redesign the architecture.

Do NOT rewrite completed phases.

Do NOT introduce new features.

Do NOT replace existing implementations without verified evidence.

Do NOT download another model.

Do NOT reinstall the manually installed model.

Do NOT overwrite the manually copied ONNX files.

Preserve compatibility with every previous phase.

Every code change must be validated by rebuilding the solution and verifying the runtime before considering this validation phase complete.