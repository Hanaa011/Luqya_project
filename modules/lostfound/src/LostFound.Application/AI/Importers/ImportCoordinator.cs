using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Concepts;
using LostFound.AI.Graph;
using LostFound.AI.Providers;

namespace LostFound.AI.Importers
{
    /// <summary>
    /// Runs the full pipeline (everything after IDatasetImporter's own
    /// Download/Integrity-Validation/Parsing) for one or more sources:
    /// Schema Validation -&gt; Normalization -&gt; Concept Extraction
    /// (Deduplication -&gt; Conflict Resolution -&gt; Canonical Concept
    /// Builder) -&gt; Relationship Extraction -&gt; Knowledge Graph
    /// Persistence -&gt; Version Registration - matching the spec's pipeline
    /// diagram exactly. "Embedding Queue" is intentionally a no-op here:
    /// embeddings are generated lazily and cached by
    /// LostFound.AI.Embeddings.LocalFirstEmbeddingEngine (Phase 2A Part 2)
    /// the first time a concept's text is actually searched, rather than
    /// eagerly during import.
    /// </summary>
    internal sealed class ImportCoordinator(
        IDataValidator validator,
        IDataNormalizer normalizer,
        IConceptBuilder conceptBuilder,
        IRelationshipBuilder relationshipBuilder,
        IKnowledgeGraph knowledgeGraph,
        IAliasResolver aliasResolver,
        IDatasetImportHistoryRepository historyRepository,
        ILogger<ImportCoordinator> logger) : IImportCoordinator
    {
        private static readonly string[] SupportedLanguages = { "ar", "en", "ur" };

        public async Task<DatasetImportRecord> ImportAsync(IDatasetImporter importer, ImportMode mode, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var buildId = Guid.NewGuid().ToString("N")[..12];

            try
            {
                // "Retry failed datasets": one source's transient fetch
                // failure (network blip, etc.) is retried with backoff
                // before being treated as a real failure - reuses the same
                // resilience building block Phase 2A Part 1 introduced for
                // AI providers.
                var snapshot = await ResilientProviderDecorator.ExecuteAsync(
                    ct => importer.FetchAsync(ct),
                    $"{importer.DatasetName} FetchAsync",
                    logger,
                    cancellationToken);

                if (mode == ImportMode.Incremental)
                {
                    var latest = await historyRepository.GetLatestSuccessfulAsync(importer.DatasetName, cancellationToken);
                    if (latest != null && latest.DatasetVersion == snapshot.DatasetVersion)
                    {
                        var skipped = new DatasetImportRecord(
                            Guid.NewGuid(), importer.DatasetName, snapshot.DatasetVersion, buildId,
                            DatasetImportStatus.Skipped, DateTime.UtcNow, 0, 0, 0, 0, stopwatch.ElapsedMilliseconds,
                            $"Dataset version '{snapshot.DatasetVersion}' already imported at {latest.ImportedAtUtc:O}.");

                        await historyRepository.RecordAsync(skipped, cancellationToken);
                        logger.LogInformation(
                            "Skipped import of '{Dataset}': version '{Version}' unchanged.", importer.DatasetName, snapshot.DatasetVersion);

                        return skipped;
                    }
                }

                // ---- Schema Validation ----
                var validConcepts = new List<RawConceptRecord>();
                var validationFailures = 0;

                foreach (var record in snapshot.Concepts)
                {
                    var validation = validator.ValidateConcept(record, SupportedLanguages);
                    if (validation.IsValid)
                    {
                        validConcepts.Add(record);
                        continue;
                    }

                    validationFailures++;
                    LogValidationIssues(importer.DatasetName, record.SourceId, validation.Issues);
                }

                // ---- Normalization ----
                var normalizedConcepts = validConcepts.Select(normalizer.Normalize).ToList();

                // ---- Concept Extraction: Deduplication -> Conflict
                // Resolution -> Canonical Concept Builder ----
                var buildResult = conceptBuilder.BuildConcepts(normalizedConcepts);
                var mergedRecordCount = normalizedConcepts.Count - buildResult.Concepts.Count;
                var knownConceptNames = buildResult.ConceptsByRawName.Keys.ToList();

                // ---- Relationship Extraction (explicit relationships from
                // the source, plus each concept's own ParentNames) ----
                var relationshipRecords = new List<RawRelationshipRecord>(snapshot.Relationships);
                foreach (var record in normalizedConcepts)
                {
                    foreach (var parentName in record.ParentNames)
                    {
                        relationshipRecords.Add(new RawRelationshipRecord(record.SourceDataset, record.CanonicalName, parentName, RelationshipType.IsA));
                    }
                }

                var validRelationships = new List<RawRelationshipRecord>();
                foreach (var relationship in relationshipRecords)
                {
                    var validation = validator.ValidateRelationship(relationship, knownConceptNames);
                    if (validation.IsValid)
                    {
                        validRelationships.Add(relationship);
                        continue;
                    }

                    validationFailures++;
                    LogValidationIssues(importer.DatasetName, $"{relationship.SourceConceptName}->{relationship.TargetConceptName}", validation.Issues);
                }

                var relationships = relationshipBuilder.BuildRelationships(validRelationships, buildResult.ConceptsByRawName);

                // ---- Knowledge Graph Persistence ----
                foreach (var concept in buildResult.Concepts)
                {
                    await knowledgeGraph.AddConceptAsync(concept, cancellationToken);
                }

                foreach (var relationship in relationships)
                {
                    await knowledgeGraph.AddRelationshipAsync(
                        relationship.SourceConceptId, relationship.TargetConceptId, relationship.RelationshipType,
                        relationship.Weight, relationship.SourceDataset, cancellationToken);
                }

                await aliasResolver.RebuildIndexAsync(cancellationToken);

                // ---- Version Registration ----
                var status = validationFailures > 0 ? DatasetImportStatus.SucceededWithWarnings : DatasetImportStatus.Succeeded;
                var importRecord = new DatasetImportRecord(
                    Guid.NewGuid(), importer.DatasetName, snapshot.DatasetVersion, buildId, status, DateTime.UtcNow,
                    buildResult.Concepts.Count, relationships.Count, mergedRecordCount, validationFailures,
                    stopwatch.ElapsedMilliseconds, null);

                await historyRepository.RecordAsync(importRecord, cancellationToken);

                logger.LogInformation(
                    "Imported '{Dataset}' v{Version}: {Concepts} concept(s), {Relationships} relationship(s), " +
                    "{Merged} record(s) merged, {Failures} validation failure(s) in {Elapsed}ms.",
                    importer.DatasetName, snapshot.DatasetVersion, buildResult.Concepts.Count, relationships.Count,
                    mergedRecordCount, validationFailures, stopwatch.ElapsedMilliseconds);

                return importRecord;
            }
            // OperationCanceledException/TaskCanceledException is only a
            // genuine "stop retrying, let it propagate" signal when it was
            // OUR OWN cancellationToken that fired (real app shutdown).
            // HttpClient.Timeout also throws TaskCanceledException - which
            // IS-A OperationCanceledException - for an ordinary slow/
            // unreachable endpoint, so excluding the whole exception family
            // here let a Wikidata timeout escape this catch, then
            // ImportAllAsync, then OnPostApplicationInitializationAsync,
            // and crash the entire host at startup instead of degrading
            // this one source to zero concepts as documented on
            // WikidataDatasetImporter. Mirrors the same caller-token check
            // ResilientProviderDecorator.IsTransient already uses.
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var failed = new DatasetImportRecord(
                    Guid.NewGuid(), importer.DatasetName, "unknown", buildId, DatasetImportStatus.Failed, DateTime.UtcNow,
                    0, 0, 0, 0, stopwatch.ElapsedMilliseconds, ex.Message);

                await historyRepository.RecordAsync(failed, cancellationToken);
                logger.LogError(ex, "Import failed for dataset '{Dataset}'.", importer.DatasetName);

                return failed;
            }
        }

        // Every importer runs independently and in parallel - one source
        // failing (caught inside ImportAsync itself) never prevents the
        // others from completing or being reflected in the report.
        public async Task<ImportReport> ImportAllAsync(IEnumerable<IDatasetImporter> importers, ImportMode mode, CancellationToken cancellationToken = default)
        {
            var results = await Task.WhenAll(importers.Select(importer => ImportAsync(importer, mode, cancellationToken)));
            return new ImportReport(results);
        }

        private void LogValidationIssues(string datasetName, string recordIdentifier, IReadOnlyList<ValidationIssue> issues)
        {
            foreach (var issue in issues)
            {
                logger.LogWarning(
                    "Validation failed for '{Dataset}' record '{RecordId}': [{Code}] {Message}",
                    datasetName, recordIdentifier, issue.Code, issue.Message);
            }
        }
    }
}
