namespace LostFound.AI.Importers
{
    // Applies LostFound.AI.Concepts.IConceptNormalizer's per-language rules
    // across every text field of a raw record - kept as its own interface
    // (rather than inlining normalizer calls into the coordinator) so the
    // "which fields get normalized and how" policy is unit-testable and
    // reusable independent of the async pipeline.
    public interface IDataNormalizer
    {
        RawConceptRecord Normalize(RawConceptRecord record);
    }
}
