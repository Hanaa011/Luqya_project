using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using LostFound.AI.Configuration;

namespace LostFound.AI.Ontology;

// TEMPORARY INVESTIGATIVE ARTIFACT - see GeneralizedConceptEquivalenceValidationTests
// and RealEmbeddingFallbackValidationTests for context. LostFoundApplicationTestModule
// deliberately points LocalAiRuntimeOptions at an empty throwaway temp directory (no
// model installed) so the ordinary test suite never pays ONNX load cost or depends on
// model files being present. This module exists ONLY to answer one specific, narrow
// validation question - does ConceptResolver's semantic-similarity fallback tier
// resolve short, multi-word ObjectType-style phrases (e.g. "Duffel Bag" vs "Bag") -
// which requires REAL embeddings from the REAL production model. It repoints
// LocalAiRuntimeOptions at the actual model files already present on disk at
// src/Forge.HttpApi.Host/AI-Models/Embeddings/ (the same files the live application
// loads), with Enabled=true, while keeping DatabasePath on a throwaway temp path so
// this never touches the real AI-Data/embeddings.db cache. No production code is
// modified by this file - it only changes what a TEST points its options at.
[DependsOn(typeof(LostFoundApplicationTestModule))]
public class RealEmbeddingFallbackTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var realModelDirectory = Path.Combine(repoRoot, "src", "Forge.HttpApi.Host", "AI-Models", "Embeddings");

        if (!Directory.Exists(realModelDirectory))
        {
            throw new InvalidOperationException(
                $"RealEmbeddingFallbackTestModule: expected real model directory not found at '{realModelDirectory}'. " +
                "This test module requires the same on-disk model the live host uses.");
        }

        var throwawayDb = Path.Combine(Path.GetTempPath(), "lostfound-real-embedding-tests", Guid.NewGuid().ToString("N"), "embeddings.db");
        Directory.CreateDirectory(Path.GetDirectoryName(throwawayDb)!);

        // Registered AFTER LostFoundApplicationTestModule's own PostConfigure (this
        // module DependsOn it, so ABP runs that module's ConfigureServices first) -
        // this PostConfigure runs later against the same options instance and wins.
        context.Services.PostConfigure<LocalAiRuntimeOptions>(options =>
        {
            options.Enabled = true;
            options.ModelDirectory = realModelDirectory;
            options.DatabasePath = throwawayDb;
            options.ModelFileName = "model.onnx";
            options.TokenizerFileName = "sentencepiece.bpe.model";
            options.EmbeddingDimensions = 1024;
            options.InputIdsTensorName = "input_ids";
            options.AttentionMaskTensorName = "attention_mask";
            options.TokenTypeIdsTensorName = "token_type_ids";
            options.OutputTensorName = "token_embeddings";
            options.MaxSequenceLength = 512;
        });
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException("RealEmbeddingFallbackTestModule: could not locate repository root (CLAUDE.md) above " + startDirectory);
        }

        return dir.FullName;
    }
}
