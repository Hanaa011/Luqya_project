using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Query
{
    // Dictionary-assisted correction via edit distance against the known
    // concept vocabulary (Phase 2A Part 3's alias index) - the spec also
    // lists "Keyboard proximity" and "Semantic correction" as techniques;
    // those aren't implemented (keyboard-layout tables and semantic/
    // embedding-based correction are real additional techniques, not
    // built here - see DictionarySpellCorrectionService's remarks).
    // "Never silently replace text" (spec) - callers get the correction
    // AND its confidence, and decide what to do with a low-confidence one.
    public interface ISpellCorrectionService
    {
        Task<IReadOnlyList<SpellCorrection>> CorrectAsync(
            IReadOnlyList<string> tokens, string languageCode, CancellationToken cancellationToken = default);
    }
}
