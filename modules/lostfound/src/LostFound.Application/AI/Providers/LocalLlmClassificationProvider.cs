using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// PHASE-VALIDATION-08 Local LLM Classification Engine. Registered as
    /// the new <see cref="ILocalClassificationProvider"/>, replacing the
    /// rule-based <see cref="LocalClassificationProvider"/> at that seam -
    /// see <see cref="LostFoundAiProvidersServiceCollectionExtensions"/>.
    ///
    /// Architecture (see the phase report's Integration section for the
    /// full justification): a local instruction-tuned LLM, served by a
    /// local Ollama daemon, is the PRIMARY local classifier - real,
    /// measured accuracy on a 40-case multilingual benchmark was
    /// substantially higher than the rule-based engine's last-resort
    /// fallback path for descriptions not already covered by the ontology
    /// (see the report's Accuracy Comparison). The rule-based
    /// <see cref="LocalClassificationProvider"/> (entity recognition +
    /// ontology/concept resolution + embedding-similarity) is NOT deleted -
    /// it is composed in as the inner fallback for when the LLM is
    /// unavailable (Ollama not installed/running), disabled
    /// (<see cref="LocalLlmOptions.Enabled"/> = false), or fails/times out.
    /// This preserves the project's foundational "must continue operating
    /// when every external AI provider is unavailable" guarantee at the
    /// LOCAL tier too - Ollama itself is a local process that can fail to
    /// be running, so the local-first posture needs its own fallback of
    /// last resort, exactly as documented for the remote-provider case in
    /// <see cref="LostFound.AI.Core.ClassificationEngine"/>.
    ///
    /// Image handling uses the "paired vision-component + text-model"
    /// architecture (option 2 from the phase spec), not a native
    /// vision-language model - see the report's Multimodal Architecture
    /// Recommendation for the measured reasoning: no text candidate
    /// evaluated here is vision-capable, and the smallest viable native
    /// VLMs this CPU-only, ~16GB-RAM hardware can run are moondream-class
    /// image captioners, not full multilingual classification models. A
    /// vision-capable model (<see cref="LocalLlmOptions.VisionModel"/>)
    /// produces a short free-text caption of the image; that caption is fed
    /// into the SAME text prompt as additional context (see
    /// <see cref="LocalLlmClassificationPromptBuilder"/>), merging image and
    /// text into one classification result rather than treating them as
    /// alternatives. An image-only report (no description) still gets a
    /// real classification from the caption alone; a failed/unclear caption
    /// degrades gracefully to text-only classification, never fails the
    /// report - see <see cref="TryCaptionImageAsync"/>.
    /// </summary>
    internal sealed class LocalLlmClassificationProvider(
        HttpClient httpClient,
        IOptions<AIProviderOptions> options,
        LocalClassificationProvider ruleBasedFallback,
        ILogger<LocalLlmClassificationProvider> logger) : ILocalClassificationProvider
    {
        private readonly LocalLlmOptions _options = options.Value.LocalLlm;

        public string ProviderName => "LocalLlm";

        public async Task<ItemClassificationResult> ClassifyAsync(
            string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                logger.LogDebug("LocalLlmClassificationProvider: disabled via configuration; using rule-based engine directly.");
                return await ruleBasedFallback.ClassifyAsync(description, imageBytes, cancellationToken);
            }

            string? imageContext = null;
            if (imageBytes is { Length: > 0 })
            {
                imageContext = await TryCaptionImageAsync(imageBytes, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(imageContext))
            {
                // No text AND no usable image caption - nothing for the LLM
                // to reason about. Same "no AI classification" degrade the
                // rule-based engine and ClassificationEngine itself already
                // use for a truly empty report.
                logger.LogInformation(
                    "LocalLlmClassificationProvider: no text description and no usable image; continuing without AI classification.");
                return new ItemClassificationResult();
            }

            try
            {
                var prompt = LocalLlmClassificationPromptBuilder.Build(description, imageContext);

                var raw = await LocalLlmOllamaClient.GenerateAsync(
                    httpClient, _options.BaseUrl, _options.Model, prompt, _options.TimeoutSeconds, cancellationToken);

                var result = ClassificationJsonParser.Parse(raw);

                if (string.IsNullOrWhiteSpace(result.ObjectType))
                {
                    // The LLM ran and returned parseable JSON, but left the
                    // single most consequential field - ObjectType, the
                    // field AiMatchingService/search weight most heavily and
                    // the one Report.AiObjectType persists directly - empty.
                    //
                    // Real E2E finding (Arabic-E2E-Matching-Validation): the
                    // original condition here required BOTH ObjectType AND
                    // CategoryName to be empty before falling back. A real
                    // production case ("لقيت بطاقة بنكية فيزا...", a Visa
                    // bank card) produced ObjectType="" but CategoryName=
                    // "Jewelry" (itself wrong) and Brand="Visa" (correct) -
                    // the AND condition never fired, so a confidently WRONG,
                    // half-empty result was accepted outright instead of
                    // giving the rule-based engine a chance. An empty
                    // ObjectType is never useful on its own regardless of
                    // what the other fields say, so it alone is now
                    // sufficient to trigger the fallback.
                    //
                    // PHASE-VALIDATION-08 real-world finding: for an
                    // image-only report (no user-typed description at all),
                    // passing the ORIGINAL null description through here
                    // meant the rule-based engine had nothing to work with
                    // either - LocalClassificationProvider requires text by
                    // design (it never processes image bytes itself) and
                    // logs exactly that ("local classification requires
                    // text") before giving up, so the report ended up fully
                    // unclassified even though a perfectly usable image
                    // caption existed. The caption IS natural-language text
                    // describing the item, so pass it through as the
                    // fallback's description whenever there is no real
                    // user-typed one - giving image-only reports a genuine
                    // second chance at ontology/entity-recognition matching
                    // instead of skipping straight to an empty result.
                    var fallbackDescription = string.IsNullOrWhiteSpace(description) ? imageContext : description;

                    logger.LogDebug(
                        "LocalLlmClassificationProvider: model '{Model}' returned no usable classification; falling back to rule-based engine{UsingCaption}.",
                        _options.Model,
                        string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(imageContext)
                            ? " using the image caption as text"
                            : string.Empty);
                    return await ruleBasedFallback.ClassifyAsync(fallbackDescription, imageBytes, cancellationToken);
                }

                logger.LogInformation(
                    "LocalLlmClassificationProvider: classified via model '{Model}'. Category={Category}, ObjectType={ObjectType}, " +
                    "Color={Color}, Brand={Brand}, ImageUsed={ImageUsed}.",
                    _options.Model, result.CategoryName, result.ObjectType, result.Color, result.Brand, imageContext != null);

                return result;
            }
            catch (LocalLlmException ex)
            {
                logger.LogWarning(
                    ex, "LocalLlmClassificationProvider: local LLM call failed; falling back to rule-based engine.");
                return await ruleBasedFallback.ClassifyAsync(description, imageBytes, cancellationToken);
            }
        }

        /// <summary>
        /// Best-effort image captioning. Any failure (no vision model
        /// configured, Ollama unreachable, timeout, an "unclear image"
        /// response) degrades to null context rather than throwing - an
        /// unreadable/irrelevant photo must never fail the report, it must
        /// just fall back to text-only classification, exactly as the
        /// phase spec's Image Evaluation Criteria requires.
        /// </summary>
        private async Task<string?> TryCaptionImageAsync(byte[] imageBytes, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.VisionModel))
            {
                return null;
            }

            try
            {
                var caption = await LocalLlmOllamaClient.CaptionImageAsync(
                    httpClient, _options.BaseUrl, _options.VisionModel, imageBytes, _options.TimeoutSeconds, cancellationToken);

                if (string.IsNullOrWhiteSpace(caption) ||
                    caption.Contains("unclear image", System.StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("LocalLlmClassificationProvider: image caption unusable; continuing text-only.");
                    return null;
                }

                logger.LogDebug("LocalLlmClassificationProvider: image caption = '{Caption}'.", caption);
                return caption;
            }
            catch (LocalLlmException ex)
            {
                logger.LogWarning(ex, "LocalLlmClassificationProvider: image captioning failed; continuing text-only.");
                return null;
            }
        }
    }
}
