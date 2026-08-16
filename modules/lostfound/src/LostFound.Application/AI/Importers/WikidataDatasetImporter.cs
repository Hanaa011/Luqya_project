using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Importers
{
    /// <summary>
    /// PHASE-VALIDATION-08. A real, live external <see cref="IDatasetImporter"/>
    /// - proof that the "pluggable external dataset" architecture the spec
    /// asks for is genuinely satisfiable, not just described. Queries
    /// Wikidata's public SPARQL endpoint (<c>query.wikidata.org/sparql</c>,
    /// no API key required) for real-world camera brands (entities with
    /// <c>P1056</c> "product or material produced" = <c>Q15328</c> "camera")
    /// and their multilingual labels, producing Brand-role
    /// <see cref="RawConceptRecord"/>s the exact same way
    /// <see cref="LostFoundSeedDataImporter"/> and
    /// <see cref="JsonLexiconDatasetImporter"/> do - from that point on this
    /// goes through the identical <see cref="IImportCoordinator"/> pipeline.
    /// No brand name is written in this file; the query determines the
    /// result set, not this class.
    ///
    /// Deliberately narrow in scope for this validation phase (one bounded
    /// query, one domain) rather than a general-purpose Wikidata crawler -
    /// see the PHASE-VALIDATION-08 report's "External Datasets to
    /// Integrate" section for why a broader live import (ConceptNet's full
    /// assertions, a Wikidata dump, Arabic WordNet) is a separate, larger,
    /// explicitly-scoped effort and not something to attempt inside this
    /// session. Extending this to another Wikidata category is a data
    /// change to <see cref="TargetQueries"/> below, not a redesign.
    ///
    /// Resilience: <see cref="IImportCoordinator.ImportAsync"/> already
    /// wraps every importer's <see cref="FetchAsync"/> in
    /// <c>ResilientProviderDecorator</c> (retry with backoff) and
    /// records a <c>Failed</c> <see cref="DatasetImportRecord"/> - never an
    /// unhandled exception - if every attempt fails, exactly like every
    /// other importer. Wikidata being unreachable degrades this one source
    /// to zero concepts; it does not affect the seed or JSON-lexicon
    /// importers running alongside it (<see cref="IImportCoordinator.ImportAllAsync"/>
    /// runs every importer independently via <c>Task.WhenAll</c>).
    /// </summary>
    internal sealed class WikidataDatasetImporter(IHttpClientFactory httpClientFactory, ILogger<WikidataDatasetImporter> logger) : IDatasetImporter
    {
        public string DatasetName => "wikidata-camera-brands";

        // Bump when a query below changes meaning (a different QID, a
        // different property) - the response CONTENT can legitimately
        // change between runs (Wikidata is a live, editable database), but
        // that is not something IImportCoordinator's version-based
        // incremental-skip should try to detect; re-running this importer
        // is cheap (one query) and safe (UpsertAsync), so operators who
        // want fresh data can simply request ImportMode.Full.
        private const string DatasetVersion = "1.0.0";

        private const string SparqlEndpoint = "https://query.wikidata.org/sparql";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        // (QID whose P1056 "product or material produced" instances we
        // want, the domain Category role tag to file the results under).
        // Adding a second target (e.g. Q186370 "headphones") is one more
        // row here - never a new class or a new pipeline stage.
        private static readonly IReadOnlyList<(string TargetQid, string TargetLabel, string CategoryRole)> TargetQueries =
        [
            ("Q15328", "camera", "Brand")
        ];

        public async Task<DatasetSnapshot> FetchAsync(CancellationToken cancellationToken = default)
        {
            var concepts = new List<RawConceptRecord>();

            foreach (var (targetQid, targetLabel, categoryRole) in TargetQueries)
            {
                var fetched = await FetchTargetAsync(targetQid, targetLabel, categoryRole, cancellationToken);
                concepts.AddRange(fetched);
            }

            logger.LogInformation(
                "WikidataDatasetImporter: fetched {Count} concept record(s) from {QueryCount} live SPARQL quer(ies).",
                concepts.Count, TargetQueries.Count);

            // No relationships: brand entities here are siblings, not a
            // hierarchy - IsA edges belong to object-type concepts (see the
            // other two importers).
            return new DatasetSnapshot(DatasetName, DatasetVersion, DateTime.UtcNow, concepts, Array.Empty<RawRelationshipRecord>());
        }

        private async Task<List<RawConceptRecord>> FetchTargetAsync(
            string targetQid, string targetLabel, string categoryRole, CancellationToken cancellationToken)
        {
            var query = $$"""
                SELECT ?item ?itemLabelEn ?itemLabelAr ?itemLabelUr WHERE {
                  ?item wdt:P1056 wd:{{targetQid}} .
                  OPTIONAL { ?item rdfs:label ?itemLabelEn . FILTER(LANG(?itemLabelEn) = "en") }
                  OPTIONAL { ?item rdfs:label ?itemLabelAr . FILTER(LANG(?itemLabelAr) = "ar") }
                  OPTIONAL { ?item rdfs:label ?itemLabelUr . FILTER(LANG(?itemLabelUr) = "ur") }
                }
                LIMIT 200
                """;

            var client = httpClientFactory.CreateClient(nameof(WikidataDatasetImporter));
            client.Timeout = RequestTimeout;
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("LostFoundOntologyImporter/1.0 (offline lost-and-found search)");
            client.DefaultRequestHeaders.Accept.TryParseAdd("application/sparql-results+json");

            var requestUri = $"{SparqlEndpoint}?query={Uri.EscapeDataString(query)}";

            using var response = await client.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var results = new List<RawConceptRecord>();
            var bindings = document.RootElement.GetProperty("results").GetProperty("bindings");

            foreach (var binding in bindings.EnumerateArray())
            {
                var itemUri = binding.GetProperty("item").GetProperty("value").GetString();
                var qid = itemUri?.Split('/').LastOrDefault();
                if (string.IsNullOrWhiteSpace(qid))
                {
                    continue;
                }

                var en = TryGetLabel(binding, "itemLabelEn");
                if (string.IsNullOrWhiteSpace(en))
                {
                    // A concept needs at least one anchor name; English is
                    // Wikidata's most reliably-populated label for this
                    // property, so a record with no English label at all
                    // is too sparse to be useful here.
                    continue;
                }

                results.Add(BuildRecord(qid, "en", en, categoryRole));

                var ar = TryGetLabel(binding, "itemLabelAr");
                if (!string.IsNullOrWhiteSpace(ar))
                {
                    results.Add(BuildRecord(qid, "ar", ar, categoryRole));
                }

                var ur = TryGetLabel(binding, "itemLabelUr");
                if (!string.IsNullOrWhiteSpace(ur))
                {
                    results.Add(BuildRecord(qid, "ur", ur, categoryRole));
                }
            }

            logger.LogInformation(
                "WikidataDatasetImporter: query for '{TargetLabel}' (wd:{TargetQid}) returned {RawCount} binding(s), " +
                "{RecordCount} record(s) with a usable English label.",
                targetLabel, targetQid, bindings.GetArrayLength(), results.Count);

            return results;
        }

        private RawConceptRecord BuildRecord(string qid, string languageCode, string name, string categoryRole) =>
            new(
                SourceDataset: DatasetName,
                SourceId: $"wikidata:{qid}",
                CanonicalName: name,
                LanguageCode: languageCode,
                Synonyms: Array.Empty<string>(),
                Aliases: Array.Empty<string>(),
                DialectWords: Array.Empty<string>(),
                CommonMisspellings: Array.Empty<string>(),
                Categories: new[] { categoryRole },
                ParentNames: Array.Empty<string>());

        private static string? TryGetLabel(JsonElement binding, string propertyName) =>
            binding.TryGetProperty(propertyName, out var element) && element.TryGetProperty("value", out var value)
                ? value.GetString()
                : null;
    }
}
