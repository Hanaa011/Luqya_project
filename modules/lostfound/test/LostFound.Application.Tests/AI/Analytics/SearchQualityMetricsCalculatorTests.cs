using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace LostFound.AI.Analytics;

// Phase 2B Part 4 - pure IR-metric math, no DI needed. No labeled
// relevance dataset exists in this environment to run these against real
// search traffic (see ISearchQualityMetricsCalculator's own remarks); these
// tests verify the formulas themselves are correct against known textbook
// examples.
public class SearchQualityMetricsCalculatorTests
{
    private readonly SearchQualityMetricsCalculator _calculator = new();

    [Fact]
    public void PrecisionAtK_counts_relevant_hits_within_the_top_K()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        var ranked = new List<Guid> { a, b, c, d };
        var relevant = new HashSet<Guid> { a, c };

        _calculator.PrecisionAtK(ranked, relevant, 4).ShouldBe(0.5);
        _calculator.PrecisionAtK(ranked, relevant, 1).ShouldBe(1.0);
    }

    [Fact]
    public void RecallAtK_divides_by_the_total_relevant_count_not_K()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var ranked = new List<Guid> { a, b, c };
        var relevant = new HashSet<Guid> { a, b, c, Guid.NewGuid() }; // 4 relevant total, only 3 retrievable here

        _calculator.RecallAtK(ranked, relevant, 3).ShouldBe(0.75);
    }

    [Fact]
    public void MeanReciprocalRank_uses_the_rank_of_the_first_relevant_hit()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        // Query 1: first relevant hit at rank 2 -> RR = 1/2.
        // Query 2: first relevant hit at rank 1 -> RR = 1/1.
        var rankedPerQuery = new List<IReadOnlyList<Guid>> { new List<Guid> { a, b }, new List<Guid> { c } };
        var relevantPerQuery = new List<IReadOnlySet<Guid>> { new HashSet<Guid> { b }, new HashSet<Guid> { c } };

        _calculator.MeanReciprocalRank(rankedPerQuery, relevantPerQuery).ShouldBe(0.75);
    }

    [Fact]
    public void Ndcg_is_1_when_the_ranking_already_matches_the_ideal_order()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var ranked = new List<Guid> { a, b, c };
        var grades = new Dictionary<Guid, double> { [a] = 3, [b] = 2, [c] = 1 };

        _calculator.Ndcg(ranked, grades, 3).ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public void Ndcg_is_less_than_1_when_the_ranking_disagrees_with_the_ideal_order()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var ranked = new List<Guid> { b, a }; // worse item ranked first
        var grades = new Dictionary<Guid, double> { [a] = 3, [b] = 1 };

        _calculator.Ndcg(ranked, grades, 2).ShouldBeLessThan(1.0);
    }

    [Fact]
    public void MeanAveragePrecision_averages_per_query_average_precision()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var rankedPerQuery = new List<IReadOnlyList<Guid>> { new List<Guid> { a, b } };
        var relevantPerQuery = new List<IReadOnlySet<Guid>> { new HashSet<Guid> { a, b } };

        // Both ranked items are relevant, in order -> AP = (1/1 + 2/2) / 2 = 1.0.
        _calculator.MeanAveragePrecision(rankedPerQuery, relevantPerQuery).ShouldBe(1.0);
    }
}
