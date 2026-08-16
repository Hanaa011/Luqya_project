using Shouldly;
using Xunit;

namespace LostFound.AI.Query;

// Arabic-Classification-Validation.md fixes: pure, dependency-free coverage
// for IIntentDetector.IsIntentWord - the exact mechanism
// LocalClassificationProvider's object-extraction fallback now uses to
// guarantee an intent/action word can never become ObjectType, regardless
// of where it sits in the sentence. Deliberately does not go through the
// full LocalClassificationProvider/embedding pipeline (see the
// implementation report's Testing Scope note) - IntentDetector has no
// dependencies, so this is fast, reliable, and exercises the real
// production class, not a mock.
public class IntentDetectorTests
{
    private readonly IntentDetector _detector = new();

    [Theory]
    [InlineData("لقيت")]
    [InlineData("وجدت")]
    [InlineData("فقدت")]
    [InlineData("ضاع")]
    [InlineData("ضاعت")]
    [InlineData("أضعت")]
    [InlineData("عثرت")]
    [InlineData("العثور")] // the content word of "تم العثور على"
    [InlineData("lost")]
    [InlineData("found")]
    public void Recognizes_every_reported_intent_word(string token)
    {
        _detector.IsIntentWord(token).ShouldBeTrue();
    }

    [Theory]
    [InlineData("ريموت")]
    [InlineData("ماوس")]
    [InlineData("قلم")]
    [InlineData("خاتم")]
    [InlineData("كاميرا")]
    [InlineData("remote")]
    [InlineData("mouse")]
    public void Does_not_flag_real_object_words_as_intent_words(string token)
    {
        _detector.IsIntentWord(token).ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_the_common_preposition_that_completes_the_found_phrase()
    {
        // "على" ("on"/"about") was deliberately NOT added alongside
        // "العثور" - see IntentDetector's own remarks: it is far too common
        // a word in unrelated sentences to safely treat as an intent
        // signal (it would corrupt Detect()'s whole-query classification,
        // not just the object-extraction fallback this fix targets).
        _detector.IsIntentWord("على").ShouldBeFalse();
    }

    [Fact]
    public void Detect_is_unaffected_by_the_new_method_or_the_new_word()
    {
        // Regression guard: adding IsIntentWord and "العثور" to FoundWords
        // must not change Detect()'s existing, already-validated behavior.
        _detector.Detect(new[] { "لقيت", "محفظة" }, "ar").Intent.ShouldBe(QueryIntent.FoundItem);
        _detector.Detect(new[] { "تم", "العثور", "على", "محفظة" }, "ar").Intent.ShouldBe(QueryIntent.FoundItem);
        _detector.Detect(new[] { "الشاحن", "موجود", "على", "الطاولة" }, "ar").Intent.ShouldBe(QueryIntent.SearchRequest);
    }
}
