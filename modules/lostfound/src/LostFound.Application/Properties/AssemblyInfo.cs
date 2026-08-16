using System.Runtime.CompilerServices;

// Lets LostFound.Application.Tests construct internal AI service
// implementations directly (e.g. HybridSearchEngine with NSubstitute-mocked
// repositories) instead of only through their public interfaces - useful
// specifically for cases needing a from-scratch instance with hand-picked
// dependencies that the shared DI container can't provide per-test (like a
// per-test mocked IReportRepository).
[assembly: InternalsVisibleTo("LostFound.Application.Tests")]
