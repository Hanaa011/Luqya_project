using System;
using System.Collections.Generic;
using LostFound.AI.Query;

namespace LostFound.AI
{
    /// <summary>
    /// Turns a raw search query (Arabic, English, or mixed) into the
    /// cleanest possible semantic string before it is handed to
    /// <see cref="IEmbeddingProvider"/>. This is query-side only - it never
    /// touches how candidate <see cref="LostFound.Reports.Report"/> embeddings
    /// were generated, since those are already stored vectors - but applying
    /// the same normalization/synonym rules consistently to every query is
    /// what makes semantically-equivalent phrasings
    /// ("ضيعت جوال ذهبي" / "فقدت ايفون ذهبي" / "Lost gold iPhone" /
    /// "gold Apple phone" / "Apple smartphone") converge on nearly identical
    /// embeddings instead of drifting apart on wording alone.
    ///
    /// Pipeline (in order):
    /// <list type="number">
    /// <item>Character-level normalization (Arabic hamza variants, alef
    /// maksura, taa marbuta, diacritics/tatweel, punctuation) via
    /// <see cref="Query.TextNormalizer"/> - the Phase 2B Part 1 service
    /// shared with the full async <see cref="IQueryPipeline"/>, rather than
    /// a second copy of the same character table.</item>
    /// <item>Lowercase (affects Latin script only - Arabic has no case).</item>
    /// <item>Drop report-intent words ("lost"/"found" and their Arabic
    /// equivalents) - see <see cref="IntentWords"/> and Task 3: intent is
    /// metadata about the report, not a property of the object, and must
    /// never be allowed to affect similarity.</item>
    /// <item>Drop filler/stopwords that carry no object information.</item>
    /// <item>Expand every recognized synonym (object name, color) to one
    /// canonical token via <see cref="SynonymMap"/>, so different ways of
    /// naming the same concept collapse onto the same word before embedding.</item>
    /// <item>Remove duplicate words (order-preserving).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Phase 2B Part 1 note: this class is now an orchestrator over a
    /// dedicated service (<see cref="Query.TextNormalizer"/>) rather than
    /// owning the character-normalization table itself. It stays
    /// synchronous/static - <see cref="AiMatchingService"/>'s call site
    /// (via <see cref="QueryProcessingCache"/>) is synchronous - so the
    /// intent-word/stopword removal and <see cref="SynonymMap"/> expansion
    /// below intentionally remain local rather than moving to the async,
    /// knowledge-graph-backed <see cref="ISemanticExpander"/>: that would
    /// require making this method async, a live-search-path integration
    /// change explicitly scoped to Phase 2B Part 4 (Production Integration),
    /// not this Part. <see cref="IQueryPipeline"/> (same namespace) is the
    /// full-power replacement Part 4 will wire <see cref="AiMatchingService"/>
    /// to use directly - at which point this class's remaining synonym/
    /// intent logic becomes retirable.
    /// </remarks>
    internal static class SearchTextProcessor
    {
        private static readonly ITextNormalizer TextNormalizer = new Query.TextNormalizer();

        // =====================================================================
        // ---- Synonym dictionary (Task 2) - easily extendable ------------------
        // =====================================================================

        /// <summary>
        /// Every variant (Arabic, English, common transliteration) maps to one
        /// canonical concept word. To add a new concept, just add more
        /// "variant -> canonical" entries below; nothing else needs to change.
        /// Keys are matched after normalization/lowercasing, so list them in
        /// their normalized form (e.g. "أ/إ/آ" already collapsed to "ا").
        /// </summary>
        private static readonly Dictionary<string, string> SynonymMap = BuildSynonymMap();

        private static Dictionary<string, string> BuildSynonymMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            void Group(string canonical, params string[] variants)
            {
                foreach (var variant in variants)
                {
                    map[variant] = canonical;
                }
            }

            // ---- Objects ----
            Group("phone", "جوال", "هاتف", "موبايل", "تلفون", "جوالي", "ايفون", "آيفون", "phone", "mobile", "smartphone", "iphone", "galaxy", "cell");
            Group("wallet", "محفظة", "محفظه", "wallet", "billfold");
            Group("car", "سيارة", "سياره", "عربية", "عربيه", "car", "vehicle", "automobile");
            Group("bag", "شنطة", "شنطه", "حقيبة", "حقيبه", "جنطة", "جنطه", "bag", "backpack", "purse");
            Group("laptop", "لابتوب", "حاسوب", "كمبيوتر", "كمبيوتر محمول", "laptop", "notebook");
            Group("keys", "مفتاح", "مفاتيح", "key", "keys", "remote");
            Group("watch", "ساعة", "ساعه", "watch");
            Group("glasses", "نظارة", "نظاره", "نظارات", "glasses", "eyeglasses", "sunglasses");
            Group("card", "بطاقة", "بطاقه", "هوية", "هويه", "id", "card", "identity");
            Group("tablet", "تابلت", "آيباد", "ايباد", "tablet", "ipad");
            Group("camera", "كاميرا", "كاميره", "camera");
            Group("umbrella", "مظلة", "مظله", "umbrella");
            Group("jewelry", "مجوهرات", "ذهب مصنوع", "jewelry", "jewellery");
            Group("bicycle", "دراجة", "دراجه", "bike", "bicycle");

            // ---- Colors ----
            Group("gold", "ذهبي", "دهبي", "ذهبى", "gold", "golden");
            Group("black", "أسود", "اسود", "black");
            Group("white", "أبيض", "ابيض", "white");
            Group("red", "أحمر", "احمر", "red");
            Group("blue", "أزرق", "ازرق", "blue");
            Group("green", "أخضر", "اخضر", "green");
            Group("gray", "رمادي", "gray", "grey");
            Group("brown", "بني", "brown");
            Group("silver", "فضي", "silver");
            Group("pink", "وردي", "pink");
            Group("purple", "بنفسجي", "purple");
            Group("orange", "برتقالي", "orange");
            Group("navy", "كحلي", "navy");

            // ---- Brands (transliteration only - canonical spelling, never
            //      invented; distinct from the object synonyms above) ----
            Group("apple", "ابل", "آبل", "أبل", "apple");
            Group("samsung", "سامسونج", "سامسونق", "samsung");

            return map;
        }

        // =====================================================================
        // ---- Intent words (Task 3) - never affect similarity -------------------
        // =====================================================================

        /// <summary>
        /// Report-intent verbs ("I lost..." / "I found..."). These describe
        /// what KIND of report this is, not what the object IS, so they are
        /// dropped entirely before embedding rather than normalized to a
        /// shared marker - keeping them out entirely is what lets a "lost
        /// iPhone" query score highly against a "found iPhone" report, which
        /// is exactly the behavior a lost-and-found search needs.
        /// </summary>
        private static readonly HashSet<string> IntentWords = new(StringComparer.Ordinal)
        {
            "ضيعت", "أضعت", "اضعت", "ضاع", "ضاعت", "ضيع", "فقدت", "فقدان", "فقد",
            "وجدت", "لقيت", "لقيته", "عثرت", "وجد",
            "lost", "lose", "losing", "misplaced",
            "found", "find", "finding"
        };

        /// <summary>
        /// Words that carry no information about the object itself. Kept
        /// intentionally small and conservative - removing too aggressively
        /// risks eating real signal (e.g. a genuine brand/model token).
        /// </summary>
        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "أنا", "انا", "تم", "هذا", "هذه", "هناك", "يوجد", "في", "من", "على",
            "الى", "إلى", "عن", "مع", "قد", "كان", "كانت", "لدي", "عندي", "به", "لها", "له",
            "a", "an", "the", "is", "was", "were", "i", "my", "me", "have", "has",
            "had", "there", "it", "this", "that", "of", "to", "in", "on", "at", "with"
        };

        // =====================================================================
        // ---- Public entry point -----------------------------------------------
        // =====================================================================

        /// <summary>
        /// Runs the full normalize -&gt; strip intent/filler -&gt; expand
        /// synonyms -&gt; dedupe pipeline described on this class. Returns an
        /// empty string (never null) for empty/whitespace-only input.
        /// </summary>
        public static string Process(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            var normalized = TextNormalizer.Normalize(rawText).ToLowerInvariant();

            var tokens = normalized.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            var seen = new HashSet<string>(tokens.Length, StringComparer.Ordinal);
            var output = new List<string>(tokens.Length);

            foreach (var token in tokens)
            {
                if (IntentWords.Contains(token) || StopWords.Contains(token))
                {
                    continue;
                }

                var canonical = SynonymMap.TryGetValue(token, out var mapped) ? mapped : token;

                if (seen.Add(canonical))
                {
                    output.Add(canonical);
                }
            }

            return string.Join(' ', output);
        }
    }
}
