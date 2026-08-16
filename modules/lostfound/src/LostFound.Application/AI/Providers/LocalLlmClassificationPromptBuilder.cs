using System.Text;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// Prompt used by <see cref="LocalLlmClassificationProvider"/>, distinct
    /// from <see cref="ClassificationPromptBuilder"/> (the remote-provider
    /// prompt). PHASE-VALIDATION-08's real, measured A/B benchmark (see the
    /// Prompt Engineering section of the phase report) found that the
    /// remote-provider prompt - which asks for the answer fields
    /// (category/objectType/...) before the reasoning fields
    /// (explanation/searchReason/tags) - made every small (3-4B parameter)
    /// local candidate leave objectType/category empty on 85%+ of cases,
    /// even when the SAME response's tags/explanation/searchText clearly
    /// identified the object. Reordering the JSON schema so the model
    /// writes its reasoning FIRST and its structured answer LAST (a
    /// lightweight, schema-level form of chain-of-thought that costs
    /// nothing extra to request) raised phi4-mini's ObjectType accuracy on
    /// the same 40-case dataset from 15% to 67.5% with zero code change -
    /// this prompt, not a larger model, was the fix. Larger/more capable
    /// local models (Gemini-class) don't need this ordering, but it never
    /// hurt their accuracy in testing either, so one prompt is used for
    /// every local LLM candidate rather than maintaining a second one.
    /// </summary>
    internal static class LocalLlmClassificationPromptBuilder
    {
        public static string Build(string? description, string? imageContext)
        {
            var sb = new StringBuilder();

            sb.Append("Analyze the following lost or found item and think step by step, ");
            sb.Append("then return ONLY valid JSON (no markdown, no text outside the JSON).\n\n");

            sb.Append("Return the JSON fields in EXACTLY this order - reasoning fields first, ");
            sb.Append("then the classification fields, so you reason before you answer:\n\n");

            sb.Append("{\n");
            sb.Append("  \"explanation\": \"one short sentence describing what the item is\",\n");
            sb.Append("  \"searchReason\": \"one short sentence explaining the cues that identify object type/color/brand\",\n");
            sb.Append("  \"tags\": [\"3-8 short attribute tags\"],\n");
            sb.Append("  \"category\": \"broad category, e.g. Wallets, Keys, Phones, Bags, Electronics, Accessories, Jewelry, Documents\",\n");
            sb.Append("  \"objectType\": \"the GENERAL object type ONLY, e.g. Smartphone, Wallet, Backpack, Key, Remote, Watch - do NOT put brand or model names here - REQUIRED, must match what you already said in explanation/tags, never leave empty if the item is identifiable\",\n");
            sb.Append("  \"color\": \"dominant stated/visible color, or empty if none stated\",\n");
            sb.Append("  \"brand\": \"stated/visible brand, or empty if none stated\",\n");
            sb.Append("  \"searchKeywords\": [\"5-10 compact keywords\"],\n");
            sb.Append("  \"searchText\": \"one compact 2-4 sentence natural-language paragraph, mixing languages naturally if the input did, including synonyms/alternate names people would actually search for\"\n");
            sb.Append("}\n\n");

            sb.Append("Rules: objectType and category must be consistent with what tags/explanation already ");
            sb.Append("describe - never leave objectType empty if you were able to describe the item in ");
            sb.Append("explanation/tags. Two descriptions of the same kind of item MUST get the same objectType ");
            sb.Append("even with different brands/models. If the description is too vague/short to identify any ");
            sb.Append("real object, leave objectType/category/color/brand ALL empty and say so in explanation - ");
            sb.Append("do not invent details or guess a plausible-sounding object.\n\n");

            if (!string.IsNullOrWhiteSpace(imageContext))
            {
                // Paired vision-component architecture (see the report's
                // Multimodal Architecture Recommendation): the vision model
                // already produced a structured caption of the image
                // separately - it is merged in as additional, explicitly
                // lower-trust context rather than re-sent as raw image
                // bytes, since none of the evaluated text candidates are
                // themselves vision-capable.
                sb.Append("An image was attached to this report. An offline image-recognition model produced ");
                sb.Append("this observation about the image - treat it as a helpful but possibly imperfect hint, ");
                sb.Append("and prefer the text description below wherever the two disagree:\n");
                sb.Append(imageContext);
                sb.Append("\n\n");
            }

            sb.Append("Description:\n");
            sb.Append(string.IsNullOrWhiteSpace(description) ? "(no text description provided)" : description);

            return sb.ToString();
        }
    }
}
