using System.Collections.Generic;

namespace LostFound.AI.Ranking
{
    // Explanation text for the platform's three natively-supported query
    // languages (ILanguageDetector today only ever returns "ar"/"en"/"ur" -
    // see HeuristicLanguageDetector). Any other/unrecognized code falls back
    // to English rather than throwing - explanations are a display
    // enhancement, never a reason for a search to fail.
    internal static class ExplanationVocabulary
    {
        private static readonly IReadOnlyDictionary<string, string> EnglishFeatureNames = new Dictionary<string, string>
        {
            ["EmbeddingSimilarity"] = "semantic similarity",
            ["Bm25Score"] = "text relevance",
            ["KnowledgeGraphSimilarity"] = "concept relationship",
            ["ObjectTypeSimilarity"] = "object type match",
            ["CategorySimilarity"] = "category match",
            ["BrandSimilarity"] = "brand match",
            ["ColorSimilarity"] = "color match",
            ["MaterialSimilarity"] = "material match",
            ["LocationSimilarity"] = "location match",
            ["TimeProximity"] = "time proximity",
            ["AliasMatch"] = "direct match",
            ["ExactMatch"] = "exact match",
            ["HistoricalSuccess"] = "historical success",
            ["Popularity"] = "popularity"
        };

        private static readonly IReadOnlyDictionary<string, string> ArabicFeatureNames = new Dictionary<string, string>
        {
            ["EmbeddingSimilarity"] = "تشابه دلالي",
            ["Bm25Score"] = "تشابه نصي",
            ["KnowledgeGraphSimilarity"] = "علاقة مفاهيمية",
            ["ObjectTypeSimilarity"] = "تطابق نوع الغرض",
            ["CategorySimilarity"] = "تطابق الفئة",
            ["BrandSimilarity"] = "تطابق العلامة التجارية",
            ["ColorSimilarity"] = "تطابق اللون",
            ["MaterialSimilarity"] = "تطابق الخامة",
            ["LocationSimilarity"] = "تطابق الموقع",
            ["TimeProximity"] = "قرب الوقت",
            ["AliasMatch"] = "تطابق مباشر",
            ["ExactMatch"] = "تطابق تام",
            ["HistoricalSuccess"] = "نجاح سابق",
            ["Popularity"] = "شيوع"
        };

        private static readonly IReadOnlyDictionary<string, string> UrduFeatureNames = new Dictionary<string, string>
        {
            ["EmbeddingSimilarity"] = "معنوی مماثلت",
            ["Bm25Score"] = "متنی مطابقت",
            ["KnowledgeGraphSimilarity"] = "تصوراتی تعلق",
            ["ObjectTypeSimilarity"] = "چیز کی قسم کی مماثلت",
            ["CategorySimilarity"] = "زمرے کی مماثلت",
            ["BrandSimilarity"] = "برانڈ کی مماثلت",
            ["ColorSimilarity"] = "رنگ کی مماثلت",
            ["MaterialSimilarity"] = "مواد کی مماثلت",
            ["LocationSimilarity"] = "مقام کی مماثلت",
            ["TimeProximity"] = "وقت کی قربت",
            ["AliasMatch"] = "براہ راست مماثلت",
            ["ExactMatch"] = "مکمل مماثلت",
            ["HistoricalSuccess"] = "سابقہ کامیابی",
            ["Popularity"] = "مقبولیت"
        };

        public static ExplanationText For(string? languageCode) => languageCode switch
        {
            "ar" => new ExplanationText(ArabicFeatureNames, "ar"),
            "ur" => new ExplanationText(UrduFeatureNames, "ur"),
            _ => new ExplanationText(EnglishFeatureNames, "en")
        };
    }

    internal sealed class ExplanationText(IReadOnlyDictionary<string, string> featureNames, string languageCode)
    {
        public string FeatureName(string key) => featureNames.GetValueOrDefault(key, key);

        public string MatchedOn(IReadOnlyList<string> signals, double confidence) => languageCode switch
        {
            "ar" => $"تم العثور على تطابق اعتمادًا على: {string.Join("، ", signals)}. درجة الثقة: {confidence:0.#}٪",
            "ur" => $"ان بنیادوں پر مماثلت ملی: {string.Join("، ", signals)}۔ اعتماد کی شرح: {confidence:0.#}٪",
            _ => $"Matched primarily on {string.Join(", ", signals)}. Confidence {confidence:0.#}%."
        };

        public string NoStrongSignals(double confidence) => languageCode switch
        {
            "ar" => $"لم يتم العثور على تطابق قوي. درجة الثقة: {confidence:0.#}٪",
            "ur" => $"کوئی مضبوط مماثلت نہیں ملی۔ اعتماد کی شرح: {confidence:0.#}٪",
            _ => $"No strong signals matched. Confidence {confidence:0.#}%."
        };

        public string RelatedCategoryReason => languageCode switch
        {
            "ar" => "نوع الغرض من فئة ذات صلة (مستنتج من الأنطولوجيا)",
            "ur" => "چیز کی قسم ایک متعلقہ زمرے سے ہے (اونٹولوجی سے اخذ کردہ)",
            _ => "object type in a related category (ontology-derived)"
        };

        public string UnrelatedCategoryReason => languageCode switch
        {
            "ar" => "نوع الغرض من فئة غير ذات صلة (مستنتج من الأنطولوجيا)",
            "ur" => "چیز کی قسم ایک غیر متعلقہ زمرے سے ہے (اونٹولوجی سے اخذ کردہ)",
            _ => "object type in an unrelated category (ontology-derived)"
        };
    }
}
