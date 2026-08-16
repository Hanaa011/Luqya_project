using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Core;
using LostFound.AI.Importers;
using LostFound.AI.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace LostFound.AI.Ontology;

// TEMPORARY INVESTIGATIVE ARTIFACT - not a permanent regression suite. Closes the one
// evidence gap left by GeneralizedConceptEquivalenceValidationTests: whether
// ConceptResolver's semantic-similarity fallback tier (MinSemanticSimilarity = 0.68,
// MinTokenCountForSemanticFallback = 2) actually resolves short, multi-word
// ObjectType-style phrases against the real seeded ontology, using the REAL production
// embedding model (loaded via RealEmbeddingFallbackTestModule - see that file for why
// this needs its own module). No production code was changed to write or run this file.
public class RealEmbeddingFallbackValidationTests : LostFoundApplicationTestBase<RealEmbeddingFallbackTestModule>
{
    private readonly ITestOutputHelper _output;

    public RealEmbeddingFallbackValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const double MinSemanticSimilarity = 0.68; // mirrors ConceptResolver's own constant exactly

    private async Task SeedRealOntologyAsync()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<IEnumerable<IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);
    }

    [Fact]
    public async Task Semantic_fallback_against_real_embeddings_for_short_compound_ObjectType_phrases()
    {
        await SeedRealOntologyAsync();

        var resolver = GetRequiredService<IConceptResolver>();
        var embeddingEngine = GetRequiredService<IEmbeddingEngine>();
        var runtime = GetRequiredService<IEmbeddingRuntime>();

        var status = await runtime.GetStatusAsync();
        _output.WriteLine($"Runtime health: {status.Health}, ActiveModelName: {status.ActiveModelName}, ActiveModelVersion: {status.ActiveModelVersion}");
        _output.WriteLine($"Runtime detail: {status.Detail}");
        _output.WriteLine($"Embedding engine in use: {embeddingEngine.EngineName}");
        _output.WriteLine("(Must start with 'Local:' - if it doesn't, the real ONNX runtime failed to load and this run is not valid evidence.)");
        _output.WriteLine("");

        _output.WriteLine("=== END-TO-END: does IConceptResolver.ResolveAsync now resolve these via semantic fallback? ===");
        await ResolveViaRealResolver("Duffel Bag", "en");
        await ResolveViaRealResolver("Gym Bag", "en");
        await ResolveViaRealResolver("Travel Document", "en");
        await ResolveViaRealResolver("Notebook Computer", "en");
        await ResolveViaRealResolver("Rucksack", "en");
        await ResolveViaRealResolver("Sunglasses", "en");

        _output.WriteLine("");
        _output.WriteLine("=== RAW COSINE SIMILARITY (candidate phrase vs the concept canonical name it should/shouldn't match) ===");
        _output.WriteLine($"(Threshold for a match: raw cosine > {MinSemanticSimilarity})");
        await RawCosine("Duffel Bag", "Bag", embeddingEngine);
        await RawCosine("Gym Bag", "Bag", embeddingEngine);
        await RawCosine("Travel Document", "Passport", embeddingEngine);
        await RawCosine("Notebook Computer", "Laptop", embeddingEngine);
        await RawCosine("Rucksack", "Backpack", embeddingEngine);
        await RawCosine("Sunglasses", "Glasses", embeddingEngine);
        _output.WriteLine("--- negative controls (should stay well below threshold) ---");
        await RawCosine("Duffel Bag", "Passport", embeddingEngine);
        await RawCosine("Travel Document", "Bag", embeddingEngine);
        await RawCosine("Notebook Computer", "Wallet", embeddingEngine);

        return;

        async Task ResolveViaRealResolver(string text, string lang)
        {
            var concept = await resolver.ResolveAsync(text, lang);
            _output.WriteLine(concept == null
                ? $"  '{text}' ({lang}) -> NULL (still unresolved even with real embeddings)"
                : $"  '{text}' ({lang}) -> '{concept.CanonicalName}' (id={concept.Id})");
        }
    }

    private async Task RawCosine(string textA, string textB, IEmbeddingEngine engine)
    {
        var a = await engine.GenerateEmbeddingAsync(textA);
        var b = await engine.GenerateEmbeddingAsync(textB);
        var score = CosineSimilarity(a, b);
        var verdict = score > MinSemanticSimilarity ? "MATCH" : "no match";
        _output.WriteLine($"  cosine('{textA}', '{textB}') = {score:0.0000}  [{verdict}]");
    }

    // Deliberately the exact same formula as ConceptResolver.CosineSimilarity (private,
    // internal class) so this test's numbers are directly comparable to what the real
    // resolver would compute.
    private static double CosineSimilarity(float[] a, float[] b)
    {
        var length = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0 || normB <= 0)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
