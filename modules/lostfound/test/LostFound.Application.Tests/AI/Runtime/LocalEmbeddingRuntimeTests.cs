using System.Threading.Tasks;
using LostFound.AI.Core;
using LostFound.AI.Runtime;
using Shouldly;
using Xunit;

namespace LostFound.AI.Runtime;

// Phase 2A Part 2 (Local AI Runtime). No model is installed in any test
// environment, so these assert the documented, expected "not installed"
// state and the resulting fallback to the external provider from Phase 2A
// Part 1 - the behavior that makes local-first embedding generation safe to
// ship before a model file is ever provisioned.
public class LocalEmbeddingRuntimeTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    [Fact]
    public async Task Runtime_reports_not_installed_when_no_model_has_been_installed()
    {
        var runtime = GetRequiredService<IEmbeddingRuntime>();

        var status = await runtime.GetStatusAsync();

        status.Health.ShouldBe(EmbeddingRuntimeHealth.NotInstalled);
        runtime.IsAvailable.ShouldBeFalse();
        runtime.ActiveModelVersion.ShouldBeNull();
    }

    [Fact]
    public void Embedding_engine_falls_back_to_the_configured_external_provider()
    {
        var engine = GetRequiredService<IEmbeddingEngine>();

        // No "LostFound:AI:Provider" is configured in the test environment,
        // so this exercises LostFoundAiProvidersServiceCollectionExtensions'
        // own documented default.
        engine.EngineName.ShouldBe("Gemini");
    }

    [Fact]
    public void Classification_engine_resolves_without_a_local_model()
    {
        // IClassificationEngine has no local-first counterpart (Part 2 is
        // embeddings-only, per the spec) - it should always resolve to the
        // Part 1 provider-based engine, unconditionally.
        var engine = GetRequiredService<IClassificationEngine>();

        engine.ShouldNotBeNull();
    }
}
