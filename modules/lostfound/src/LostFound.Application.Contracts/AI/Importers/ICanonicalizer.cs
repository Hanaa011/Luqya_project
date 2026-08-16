namespace LostFound.AI.Importers
{
    // Merges one DuplicateGroup into a single canonical LostFound.AI.Concepts.Concept -
    // e.g. شنطة/شنطه/حقيبة/Bag/Backpack/Handbag becoming one linked concept
    // (the spec's own example), not duplicate objects.
    public interface ICanonicalizer
    {
        Concepts.Concept Canonicalize(DuplicateGroup group);
    }
}
