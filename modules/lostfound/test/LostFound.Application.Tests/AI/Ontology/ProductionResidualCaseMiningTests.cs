using System.Collections.Generic;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Importers;
using Xunit;
using Xunit.Abstractions;

namespace LostFound.AI.Ontology;

// TEMPORARY INVESTIGATIVE ARTIFACT - not a permanent regression suite. Checks
// tier 4 (ontology) resolution for real candidate pairs mined from the live
// production database, as part of the Tier-5-target-population investigation
// (see SemanticReports/Production-Data-Mining-For-Tier-5-Residual-Cases...).
// No production code was changed to write or run this file.
public class ProductionResidualCaseMiningTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    private readonly ITestOutputHelper _output;

    public ProductionResidualCaseMiningTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private async Task SeedRealOntologyAsync()
    {
        var coordinator = GetRequiredService<IImportCoordinator>();
        var importers = GetRequiredService<IEnumerable<IDatasetImporter>>();
        await coordinator.ImportAllAsync(importers, ImportMode.Full);
    }

    [Fact]
    public async Task Tier4_resolution_for_real_mined_candidate_pairs()
    {
        await SeedRealOntologyAsync();

        var resolver = GetRequiredService<IConceptResolver>();
        var compatibility = GetRequiredService<IObjectTypeCompatibilityService>();

        var pairs = new (string QueryType, string CandidateType)[]
        {
            ("Wristwatch", "Watch"),
            ("Purse", "Coin Purse"),
            ("Eyeglasses", "Glasses"),
            ("Bicycle", "Kids Bicycle"),
            ("Cap", "Beanie"),
        };

        foreach (var pair in pairs)
        {
            var queryConcept = await resolver.ResolveAsync(pair.QueryType, "en");
            var candidateConcept = await resolver.ResolveAsync(pair.CandidateType, "en");
            var result = await compatibility.ClassifyAsync(queryConcept?.Id, pair.CandidateType, "en");

            _output.WriteLine(
                $"Query='{pair.QueryType}' -> {(queryConcept == null ? "NULL" : queryConcept.CanonicalName)} | " +
                $"Candidate='{pair.CandidateType}' -> {(candidateConcept == null ? "NULL" : candidateConcept.CanonicalName)} | " +
                $"Compatibility={result}");
        }
    }
}
