using LostFound.AI.Graph;

namespace LostFound.AI.Importers
{
    public sealed record RawRelationshipRecord(
        string SourceDataset,
        string SourceConceptName,
        string TargetConceptName,
        RelationshipType RelationshipType,
        double Weight = 1.0);
}
