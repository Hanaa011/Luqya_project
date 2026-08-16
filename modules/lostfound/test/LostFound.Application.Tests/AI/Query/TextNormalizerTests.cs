using Shouldly;
using Xunit;

namespace LostFound.AI.Query;

// SearchTextProcessor (the live search path) now delegates its character-
// level normalization to ITextNormalizer - this verifies the exact
// character table it depends on, which used to be untested inline logic.
public class TextNormalizerTests : LostFoundApplicationTestBase<LostFoundApplicationTestModule>
{
    [Theory]
    [InlineData("أضعت", "اضعت")] // hamza-on-alef variants collapse to bare alef
    [InlineData("مؤتمر", "موتمر")] // waw-hamza -> bare waw
    [InlineData("مسئول", "مسيول")] // yaa-hamza -> bare yaa
    [InlineData("مقهى", "مقهي")] // alef maksura -> yaa
    [InlineData("مدرسة", "مدرسه")] // taa marbuta -> haa
    [InlineData("ضَاعَ", "ضاع")] // diacritics stripped
    public void Normalizes_Arabic_letter_variants_to_one_canonical_form(string input, string expected)
    {
        var normalizer = GetRequiredService<ITextNormalizer>();

        normalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Strips_punctuation_to_spaces_while_preserving_mixed_script_text()
    {
        var normalizer = GetRequiredService<ITextNormalizer>();

        normalizer.Normalize("gold-iPhone, ايفون!").ShouldBe("gold iPhone  ايفون ");
    }
}
