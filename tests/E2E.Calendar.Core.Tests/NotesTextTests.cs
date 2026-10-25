using E2E.Calendar.Core;
using FluentAssertions;
using Xunit;

namespace E2E.Calendar.Core.Tests;

public sealed class NotesTextTests
{
    [Fact]
    public void SanitizeHtml_strips_script_blocks_with_their_content()
    {
        var result = NotesText.SanitizeHtml("<p>Hi</p><script>alert('x')</script>");

        result.Should().Be("<p>Hi</p>");
    }

    [Fact]
    public void SanitizeHtml_strips_unclosed_blocked_elements()
    {
        var result = NotesText.SanitizeHtml("<p>Hi</p><iframe src=\"https://evil.test\">");

        result.Should().Be("<p>Hi</p>");
    }

    [Fact]
    public void SanitizeHtml_strips_inline_event_handlers()
    {
        var result = NotesText.SanitizeHtml("<b onclick=\"steal()\">bold</b>");

        result.Should().Be("<b>bold</b>");
    }

    [Fact]
    public void SanitizeHtml_strips_style_attributes()
    {
        var result = NotesText.SanitizeHtml("<span style=\"position:fixed\">x</span>");

        result.Should().Be("<span>x</span>");
    }

    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"JaVa\tScRiPt:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<img src=\"data:text/html;base64,AAA\">", "<img>")]
    public void SanitizeHtml_removes_dangerous_urls(string input, string expected)
    {
        NotesText.SanitizeHtml(input).Should().Be(expected);
    }

    [Fact]
    public void SanitizeHtml_keeps_safe_urls_and_formatting()
    {
        var result = NotesText.SanitizeHtml("<a href=\"https://example.test/a\">link</a><b>bold</b>");

        result.Should().Be("<a href=\"https://example.test/a\">link</a><b>bold</b>");
    }

    [Fact]
    public void SanitizeHtml_keeps_image_data_urls()
    {
        var result = NotesText.SanitizeHtml("<img src=\"data:image/png;base64,AAA\">");

        result.Should().Be("<img src=\"data:image/png;base64,AAA\">");
    }

    [Fact]
    public void SanitizeHtml_returns_empty_for_blank_input()
    {
        NotesText.SanitizeHtml(null).Should().BeEmpty();
        NotesText.SanitizeHtml("   ").Should().BeEmpty();
    }

    [Fact]
    public void ToPlainText_flattens_markup_and_decodes_entities()
    {
        var result = NotesText.ToPlainText("<p>Tom &amp; Jerry</p><p>second<br>line</p>");

        result.Should().Be("Tom & Jerry\nsecond\nline");
    }

    [Fact]
    public void Plain_authored_notes_survive_a_round_trip()
    {
        const string notes = "Call Ana\nBring the Q3 deck";

        var html = NotesText.RtfToHtml(NotesText.HtmlToRtf(notes));

        NotesText.ToPlainText(html).Should().Be(notes);
    }

    [Fact]
    public void Bold_and_italic_survive_a_round_trip()
    {
        var html = NotesText.RtfToHtml(NotesText.HtmlToRtf("a<b>b</b><i>i</i>"));

        html.Should().Be("a<b>b</b><i>i</i>");
    }

    [Fact]
    public void Non_ascii_characters_survive_a_round_trip()
    {
        var html = NotesText.RtfToHtml(NotesText.HtmlToRtf("caf\u00e9 \u2014 na\u00efve"));

        html.Should().Be("caf\u00e9 \u2014 na\u00efve");
    }

    [Fact]
    public void Unsupported_markup_is_dropped_but_text_is_kept()
    {
        var html = NotesText.RtfToHtml(NotesText.HtmlToRtf("keep<table><tr><td>this</td></tr></table>"));

        NotesText.ToPlainText(html).Should().Be("keepthis");
    }

    [Fact]
    public void Sanitising_editor_output_leaves_no_script_payload()
    {
        var html = NotesText.RtfToHtml(NotesText.HtmlToRtf("hi"));

        NotesText.SanitizeHtml(html).Should().NotContain("script");
    }
}
