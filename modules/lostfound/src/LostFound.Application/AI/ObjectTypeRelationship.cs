using System;
using System.Collections.Generic;

namespace LostFound.AI
{
    /// <summary>
    /// Buckets normalized object-type strings into broad "clusters" so a
    /// mismatch can be penalized proportionally to how unrelated the two
    /// object types actually are, instead of one flat penalty for every
    /// disagreement. Also backs the early-filtering shortcut in
    /// <see cref="AiMatchingService"/> (Task 8) - both use the exact same
    /// table, so "what counts as unrelated" is defined in one place.
    ///
    /// Deliberately conservative: object types the table doesn't recognize
    /// are never penalized beyond the flat default, since guessing wrong
    /// here would incorrectly bury a genuine match. Clustering only sharpens
    /// the penalty for pairs we're confident about.
    /// </summary>
    internal static class ObjectTypeRelationship
    {
        public enum Relationship
        {
            /// <summary>Same normalized object type - no penalty applies (a bonus applies instead).</summary>
            Same,

            /// <summary>Different object type, but within the same broad cluster (e.g. Phone vs Wallet).</summary>
            RelatedCluster,

            /// <summary>Different object type in unrecognized/unclustered categories - flat default penalty.</summary>
            Unknown,

            /// <summary>Different object type in clearly unrelated clusters (e.g. Phone vs Car, Laptop vs Phone).</summary>
            UnrelatedCluster
        }

        /// <summary>
        /// Cluster membership, keyed by canonical object-type word (already
        /// lowercased/singularized the same way <see cref="LostFound.Reports.Report.NormalizeAttribute"/>
        /// would). Laptops/tablets/cameras are deliberately their own cluster,
        /// separate from phones, even though both are "electronics" - a lost
        /// laptop report matching a found-phone report is still a bad match,
        /// per the "Laptop vs Phone -&gt; Huge penalty" requirement.
        /// </summary>
        private static readonly Dictionary<string, string> ClusterByObjectType = BuildClusters();

        private static Dictionary<string, string> BuildClusters()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Cluster(string clusterName, params string[] objectTypes)
            {
                foreach (var type in objectTypes)
                {
                    map[type] = clusterName;
                }
            }

            // Vocabulary matches the exact objectType values ClassificationPromptBuilder
            // asks the model for (e.g. "Smartphone", "Wallet", "Car", "Backpack",
            // "Key") - normalized to lowercase the same way Report.NormalizeAttribute
            // would. A few defensive synonyms are included in case a provider drifts
            // from the requested vocabulary.
            Cluster("mobile", "smartphone", "phone", "mobile");
            Cluster("computing", "laptop", "tablet", "camera", "notebook");
            Cluster("small-items", "wallet", "key", "keys", "car key", "house key", "watch", "jewelry", "ring", "necklace");
            Cluster("bags", "backpack", "bag", "purse", "handbag");
            Cluster("vehicles", "car", "bicycle", "bike", "motorcycle", "scooter", "vehicle");

            // Real E2E finding (Search<->Matching Ranking Unification
            // validation): this table originally covered only 5 clusters /
            // ~20 words, so most of a real Arabic lost-and-found dataset's
            // categories (documents, bank cards, clothes, shoes, pens,
            // headphones, chargers, books) fell through to Unknown - the
            // SAME systemic gap independently discovered in the ontology-
            // based IObjectTypeCompatibilityService (a "Jacket" query never
            // resolves to any knowledge-graph concept, so ITS compatibility
            // check also always returns Unknown - see RankingEngine's use of
            // this table as a fallback for exactly that case). Extended here
            // using the actual objectType values ClassificationPromptBuilder's
            // vocabulary produced for a real validation dataset (multi-word
            // values like "Bank Card"/"Car Key"/"ID Card" normalize to their
            // lowercased, space-preserving form - see Report.NormalizeAttribute
            // - so they are listed here exactly as they appear, not merged
            // into single-word approximations that would never match).
            Cluster("documents", "passport", "certificate", "document");
            Cluster("cards", "card", "bank card", "id card", "credit card", "debit card");
            Cluster("clothes", "jacket", "coat", "sweater", "clothes", "clothing");
            Cluster("shoes", "shoes", "shoe", "sneakers", "boots");
            Cluster("writing", "pen", "pencil", "marker");
            Cluster("audio", "headphones", "earbuds", "earphones", "airpods");
            Cluster("chargers", "charger", "cable", "power bank");
            Cluster("books", "book", "textbook", "novel");
            Cluster("eyewear", "glasses", "sunglasses");

            return map;
        }

        /// <summary>
        /// Classifies how related two (already-normalized, non-empty,
        /// non-equal) object types are. Callers are expected to have already
        /// handled the "same type" and "either side missing" cases.
        /// </summary>
        public static Relationship Classify(string normalizedCandidateType, string normalizedQueryType)
        {
            var hasCandidateCluster = ClusterByObjectType.TryGetValue(normalizedCandidateType, out var candidateCluster);
            var hasQueryCluster = ClusterByObjectType.TryGetValue(normalizedQueryType, out var queryCluster);

            if (!hasCandidateCluster || !hasQueryCluster)
            {
                return Relationship.Unknown;
            }

            return string.Equals(candidateCluster, queryCluster, StringComparison.OrdinalIgnoreCase)
                ? Relationship.RelatedCluster
                : Relationship.UnrelatedCluster;
        }
    }
}
