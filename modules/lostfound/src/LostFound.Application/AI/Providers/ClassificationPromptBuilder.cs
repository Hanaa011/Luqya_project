using System.Text;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Builds the classification prompt sent to every text-generation-capable
    /// provider (Gemini, OpenAI, Ollama, DeepSeek). Centralized so all
    /// providers ask for the exact same JSON shape and the exact same
    /// <c>searchText</c> quality bar - previously this text was duplicated
    /// per-provider, which is how providers silently drifted out of parity
    /// with each other. Providers with no flexible text-generation model
    /// (e.g. HuggingFace's free zero-shot inference API) do not use this -
    /// see <see cref="HuggingFaceClassificationProvider"/> for why.
    /// </summary>
    internal static class ClassificationPromptBuilder
    {
        public static string Build(string? description)
        {
            var sb = new StringBuilder();

            sb.Append("Analyze the following lost or found item.\n\n");
            sb.Append("Return ONLY valid JSON.\n");
            sb.Append("Do not return markdown.\n");
            sb.Append("Do not return explanations outside the JSON.\n\n");
            sb.Append("JSON format:\n\n");
            sb.Append("{\n");
            sb.Append("  \"category\": \"\",\n");
            sb.Append("  \"objectType\": \"\",\n");
            sb.Append("  \"color\": \"\",\n");
            sb.Append("  \"brand\": \"\",\n");
            sb.Append("  \"tags\": [],\n");
            sb.Append("  \"explanation\": \"\",\n");
            sb.Append("  \"searchReason\": \"\",\n");
            sb.Append("  \"searchKeywords\": [],\n");
            sb.Append("  \"searchText\": \"\"\n");
            sb.Append("}\n\n");

            sb.Append("Field guidance:\n");
            sb.Append("- category: broad category (e.g. \"Wallets\", \"Keys\", \"Phones\", \"Bags\").\n");
            sb.Append("- objectType: the GENERAL object category ONLY - e.g. \"Smartphone\", \"Wallet\", \"Car\", \"Backpack\", \"Key\". Do NOT put brand or model names here (put those in \"brand\" instead). This matters a lot: two descriptions of the same kind of item MUST get the exact same objectType even if they mention different brands/models - e.g. an iPhone and a Samsung Galaxy phone must BOTH be classified as objectType \"Smartphone\", with \"Apple\"/\"Samsung\" going in \"brand\" separately. Inconsistent objectType granularity (e.g. sometimes \"iPhone\", sometimes \"Smartphone\", for the same kind of item) breaks downstream matching, so always pick the most general common category term for the object's kind.\n");
            sb.Append("- color: the dominant visible color, if any.\n");
            sb.Append("- brand: the visible or stated brand, if any.\n");
            sb.Append("- tags: 3-8 short structured attribute tags.\n");
            sb.Append("- explanation: one short, human-readable sentence describing what you saw/understood. This is for display, not for matching.\n");
            sb.Append("- searchReason: one short sentence explaining WHY you chose this classification (the cues that drove it). For display/debugging only.\n");
            sb.Append("- searchKeywords: 5-10 compact keywords (not a paragraph), useful for keyword filters. Not used for embedding.\n\n");

            sb.Append("searchText is the MOST IMPORTANT field - read these rules carefully:\n");
            sb.Append("- Do NOT simply concatenate the other fields (no \"wallet, black, leather, Samsung\" style lists).\n");
            sb.Append("- Instead, write ONE compact, natural-language paragraph describing the object exactly as a human would describe it to a friend.\n");
            sb.Append("- Naturally weave in: visible characteristics, object type, color, material (if visible), brand (if visible), distinguishing marks, and likely/common usage.\n");
            sb.Append("- Include common alternative names, synonyms, and common search phrases people would actually type - not just the formal name.\n");
            sb.Append("- Understand regional and dialect wording, not just standard dictionary terms. For example:\n");
            sb.Append("    Arabic bag terms: شنطة, حقيبة, جنطة\n");
            sb.Append("    English wallet terms: wallet, billfold, card holder\n");
            sb.Append("    Key terms: key, car key, house key, remote key\n");
            sb.Append("    Phone terms: mobile, cell phone, iphone, galaxy\n");
            sb.Append("  Apply this same style of regional/synonym thinking to whatever object is actually described, not only these examples.\n");
            sb.Append("- If the source description or image contains Arabic and English naturally, let searchText mix both naturally, the way a bilingual speaker would - do not force a translation into a single language.\n");
            sb.Append("- If a proper name (brand, model, or specific label) appears in the description, preserve it EXACTLY as written - do not transliterate, translate, or alter its spelling.\n");
            sb.Append("- searchText is optimized for semantic embedding and matching, NOT for display. It does not need to be pretty; it needs to be information-dense and use the vocabulary a real searcher would use.\n");
            sb.Append("- Keep it compact: roughly 2-4 sentences. Do not repeat the same fact twice.\n\n");

            sb.Append("Description:\n");
            sb.Append(description ?? "(Image only)");

            return sb.ToString();
        }
    }
}
