using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Query
{
    // Dictionary+pattern based extraction: Object/Color/Brand/Material/
    // Category come from matching tokens against the knowledge graph's
    // known concept vocabulary (Phase 2A Part 3's Concept.Colors/Brands/
    // Materials/Categories fields - real data, not invented); Location/
    // DateTime/Quantity come from regex patterns. This is classic rule-
    // based NER, appropriate for a closed, curated domain vocabulary - not
    // a trained sequence-labeling model.
    public interface IEntityRecognizer
    {
        Task<IReadOnlyList<RecognizedEntity>> RecognizeAsync(
            IReadOnlyList<string> tokens, string languageCode, CancellationToken cancellationToken = default);
    }
}
