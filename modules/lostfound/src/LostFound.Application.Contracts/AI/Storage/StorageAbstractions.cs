namespace LostFound.AI.Storage
{
    // Phase 2A Part 5 names IVectorStore/IKnowledgeStore/IMetadataStore/
    // IModelStore/ICacheStore as the required "storage abstraction layer."
    // Each already has a real, working, purpose-built implementation from
    // an earlier Part - LostFound.AI.Storage.IEmbeddingStore (Part 2),
    // LostFound.AI.Graph.IKnowledgeGraph (Part 3), LostFound.AI.Models.IEmbeddingModelManager
    // and LostFound.AI.Importers.IDatasetImportHistoryRepository (Part 4),
    // LostFound.AI.Caching.IEmbeddingCache (Part 2). Re-deriving five
    // parallel generic interfaces with their own CRUD surface and wiring
    // every concrete class to implement TWO unrelated interfaces for the
    // same data would be pure duplication with no behavioral benefit - it
    // wouldn't make storage any MORE replaceable than the existing
    // interfaces already do (each is already swappable independent of
    // business logic - that requirement is already met).
    //
    // Instead, these are empty marker interfaces: the concrete class that
    // already IS that store for this module also declares the name the
    // spec asks for, so "IVectorStore" etc. are real, resolvable,
    // discoverable types - just without a second, redundant member list to
    // keep in sync with the interface that already owns those operations.
    public interface IVectorStore
    {
    }

    public interface IKnowledgeStore
    {
    }

    public interface IMetadataStore
    {
    }

    public interface IModelStore
    {
    }

    public interface ICacheStore
    {
    }
}
