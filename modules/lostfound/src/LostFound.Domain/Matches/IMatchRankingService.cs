using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LostFound.Reports;

namespace LostFound.Matches
{
    /// <summary>
    /// Architectural seam that lets <see cref="MatchManager"/> (Domain)
    /// score candidate reports using the EXACT SAME ranking pipeline real
    /// Search uses (query understanding -&gt; hybrid retrieval -&gt; feature
    /// scoring -&gt; metadata penalties -&gt; confidence calibration), without
    /// the Domain layer depending on the Application layer.
    ///
    /// Why this exists (Arabic-E2E-Matching-Accuracy-Validation-Report.md
    /// finding): before this seam existed, <see cref="MatchManager"/>
    /// computed matches with its own, separate raw-cosine-similarity
    /// calculation, while <c>AiSearchAppService</c> ranked the exact same
    /// two reports through <c>ISemanticSearchOrchestrator</c> -&gt;
    /// <c>IRankingEngine</c> (feature extraction, object-type/category/
    /// brand/color metadata penalties, confidence calibration). The two
    /// paths could - and, with real evidence, DID - reach different
    /// conclusions about the same pair: Search's ranking correctly
    /// distinguished an unrelated report from a real match, while
    /// MatchManager's raw similarity alone could not (a found bicycle
    /// scored 91% "similarity" against an unrelated lost wallet and
    /// generated a real false-positive match + notification). A prior,
    /// narrower fix added an ad-hoc category-mismatch penalty directly to
    /// MatchManager to mitigate this - that penalty is now removed in
    /// favor of this seam, which eliminates the inconsistency at its root
    /// (one ranking pipeline, not two) instead of layering a second,
    /// separately-maintained penalty system on top of the first.
    ///
    /// Implemented in the Application layer (see
    /// LostFound.Application/Matches/SearchBasedMatchRankingService.cs),
    /// which wraps the already-existing <c>ISemanticSearchOrchestrator</c> -
    /// no new ranking logic is introduced, and nothing here duplicates any
    /// part of that pipeline. Domain only ever sees this interface and
    /// <see cref="Report"/> (an entity it already owns), so no Domain ->
    /// Application dependency is introduced - this mirrors the exact
    /// pattern already used for <see cref="IMatchRepository"/>/
    /// <see cref="IReportRepository"/> (Domain defines the abstraction,
    /// an outer layer implements it, DI wires them together).
    /// </summary>
    public interface IMatchRankingService
    {
        /// <summary>
        /// Ranks every opposite-type report the shared pipeline considers a
        /// plausible textual match for <paramref name="report"/>, keyed by
        /// report Id, using the report's own stored classification/
        /// description text as the query - i.e. exactly the confidence
        /// score a real user would see if they searched using this
        /// report's own text and the candidate appeared in the results. A
        /// candidate absent from the returned dictionary is one the shared
        /// retrieval/ranking pipeline itself found no meaningful signal
        /// for - genuinely not surfaced, not "scored zero", exactly as a
        /// real search would behave for that candidate.
        ///
        /// Text-only, matching <c>ISemanticSearchOrchestrator</c>'s own
        /// scope - image similarity is intentionally NOT part of this
        /// result (Search itself has no image-ranking capability to be
        /// consistent with); <see cref="MatchManager"/> blends in image
        /// similarity separately, on top of this text score, exactly where
        /// it already did before this seam existed.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, double>> RankCandidatesAsync(
            Report report, CancellationToken cancellationToken = default);
    }
}
