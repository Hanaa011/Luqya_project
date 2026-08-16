using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Concepts
{
    // Durable source of truth for concepts (SQLite-backed by default - see
    // Phase 1 Part 6's storage decision). Hot-path lookups go through
    // IAliasResolver's in-memory index instead of this repository directly -
    // see InMemoryAliasResolver.
    public interface IConceptRepository
    {
        Task<Concept?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<Concept?> FindByCanonicalNameAsync(string canonicalName, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Concept>> GetAllAsync(CancellationToken cancellationToken = default);

        // Inserts a new concept, or updates an existing one (matched by Id)
        // and archives the previous version into history - see
        // GetHistoryAsync/RollbackAsync.
        Task UpsertAsync(Concept concept, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Concept>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);

        // Restores a concept to a previously-archived version, itself
        // recorded as a new history entry (rollback is never destructive to
        // the audit trail).
        Task RollbackAsync(Guid id, int version, CancellationToken cancellationToken = default);

        Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
