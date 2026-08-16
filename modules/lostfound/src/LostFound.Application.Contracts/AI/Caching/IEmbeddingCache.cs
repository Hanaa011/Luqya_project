namespace LostFound.AI.Caching
{
    // Fast, in-process, non-persistent embedding cache - the hot-path
    // complement to IEmbeddingStore (which is slower but survives restarts).
    // "Embeddings must never be regenerated if a valid cached version
    // exists" (Part 2 spec) is satisfied by checking this first, then
    // IEmbeddingStore, before ever calling the local runtime.
    public interface IEmbeddingCache
    {
        int Count { get; }

        bool TryGet(string cacheKey, out float[]? embedding);

        void Set(string cacheKey, float[] embedding);
    }
}
