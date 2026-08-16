using System;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using Shouldly;
using Xunit;

namespace LostFound.AI.Caching;

// Phase 2A Part 5's "Concept Cache" layer.
public class ConceptCacheTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    [Fact]
    public async Task Repeated_lookups_are_served_from_cache_after_the_first_read()
    {
        var repository = GetRequiredService<IConceptRepository>();
        var cache = GetRequiredService<IConceptCache>();
        var id = Guid.NewGuid();

        await repository.UpsertAsync(new Concept { Id = id, CanonicalName = "Cached Concept" });

        // UpsertAsync write-throughs to the cache immediately.
        cache.Count.ShouldBe(1);
        cache.TryGet(id, out var cached).ShouldBeTrue();
        cached!.CanonicalName.ShouldBe("Cached Concept");

        (await repository.GetByIdAsync(id))!.CanonicalName.ShouldBe("Cached Concept");
    }

    [Fact]
    public async Task Rollback_invalidates_the_cached_entry()
    {
        var repository = GetRequiredService<IConceptRepository>();
        var cache = GetRequiredService<IConceptCache>();
        var id = Guid.NewGuid();

        await repository.UpsertAsync(new Concept { Id = id, CanonicalName = "V1", Version = 1 });
        await repository.UpsertAsync(new Concept { Id = id, CanonicalName = "V2", Version = 2 });
        cache.TryGet(id, out var beforeRollback).ShouldBeTrue();
        beforeRollback!.CanonicalName.ShouldBe("V2");

        await repository.RollbackAsync(id, 1);

        // Invalidated, not stale - the next read must go back to storage.
        cache.TryGet(id, out _).ShouldBeFalse();
        (await repository.GetByIdAsync(id))!.CanonicalName.ShouldBe("V1");
    }
}
