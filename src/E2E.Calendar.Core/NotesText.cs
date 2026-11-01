using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace E2E.Calendar.Core;

/// <summary>
/// Event notes are stored as sanitised HTML. This type owns the two things the app
/// needs for that: stripping anything executable from stored markup, and the
/// HTML &lt;-&gt; RTF translation used by the rich-text editor.
/// Pure string work so it stays unit-testable and free of UI references.
/// </summary>
public static class NotesText
{
    /// <summary>RTF destinations whose contents carry no user-visible text.</summary>
    private static readonly HashSet<string> SkippedDestinations = new(StringComparer.Ordinal)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "shppict", "nonshppict",
        "header", "headerl", "headerr", "headerf", "footer", "footerl", "footerr", "footerf",
        "footnote", "annotation", "atnid", "atnauthor", "generator", "listtable",
        "listoverridetable", "listtext", "pntext", "pnseclvl", "rsidtable", "rsidtbl",
        "themedata", "colorschememapping", "latentstyles", "datastore", "xmlnstbl",
        "filetbl", "revtbl", "uctable", "pgptbl", "bkmkstart", "bkmkend", "fldinst",
    };

    private static readonly string[] BlockedElements =
    {
        "script", "style", "iframe", "object", "embed", "applet", "template",
        "svg", "math", "noscript", "form", "video", "audio", "source",
    };

    private static readonly string[] VoidBlockedElements =
    {
        "link", "meta", "base", "input", "button", "select", "textarea", "track",
    };

    private static readonly Regex CommentRegex = new(
        "<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex BlockedElementRegex = new(
        $@"<({string.Join('|', BlockedElements)})\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex StrayBlockedTagRegex = new(
        $@"</?(?:{string.Join('|', BlockedElements)}|{string.Join('|', VoidBlockedElements)})\b[^>]*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventHandlerAttributeRegex = new(
        @"\son[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleAttributeRegex = new(
        @"\sstyle\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UrlAttributeRegex = new(
        @"(?<attr>\s+\b(?:href|src|xlink:href|action|formaction)\s*=\s*)(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bq>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BreakTagRegex = new(
        @"<br\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BlockEndTagRegex = new(
        @"</(?:p|div|li|tr|h[1-6]|blockquote|section|article)\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        @"<[^>]*>",
        RegexOptions.Compiled);

    private static readonly Regex ExcessiveNewlinesRegex = new(
        @"\n{3,}",
        RegexOptions.Compiled);

    /// <summary>Returns the notes stripped of anything that can execute when rendered.</summary>
    public static string SanitizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var value = CommentRegex.Replace(html, string.Empty);
        value = BlockedElementRegex.Replace(value, string.Empty);
        value = StrayBlockedTagRegex.Replace(value, string.Empty);

        // Attribute scrubbing is scoped to tag bodies so that ordinary note text
        // containing something like " on = " is never mistaken for a handler.
        value = TagRegex.Replace(value, CleanTag);
        return value.Trim();
    }

    private static string CleanTag(Match match)
    {
        var tag = EventHandlerAttributeRegex.Replace(match.Value, string.Empty);
        tag = StyleAttributeRegex.Replace(tag, string.Empty);
        return UrlAttributeRegex.Replace(tag, SanitizeUrlAttribute);
    }

    /// <summary>Flattens stored notes into plain text (used for search and link detection).</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var value = BreakTagRegex.Replace(html, "\n");
        value = BlockEndTagRegex.Replace(value, "\n");
        value = TagRegex.Replace(value, string.Empty);
        value = WebUtility.HtmlDecode(value);
        value = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        value = ExcessiveNewlinesRegex.Replace(value, "\n\n");
        return value.Trim();
    }

    /// <summary>Builds the RTF the rich-text editor consumes from stored HTML.</summary>
    public static string HtmlToRtf(string? html)
    {
        var builder = new StringBuilder(256);
        builder.Append(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}\viewkind4\uc1\pard\f0\fs22 ");

        if (!string.IsNullOrEmpty(html))
        {
            var bold = false;
            var italic = false;
            var underline = false;
            var strike = false;
            var wroteParagraph = false;
            var index = 0;

            while (index < html.Length)
            {
                var c = html[index];

                if (c == '<')
                {
                    var end = html.IndexOf('>', index);
                    if (end < 0)
                    {
                        break;
                    }

                    var tag = html[(index + 1)..end].Trim();
                    index = end + 1;

                    var closing = tag.StartsWith('/');
                    if (closing)
                    {
                        tag = tag[1..];
                    }

                    var separator = tag.IndexOfAny([' ', '\t', '\r', '\n', '/']);
                    if (separator >= 0)
                    {
                        tag = tag[..separator];
                    }

                    switch (tag.ToLowerInvariant())
                    {
                        case "b":
                        case "strong":
                            bold = !closing;
                            EmitFormat(builder, bold, italic, underline, strike);
                            break;
                        case "i":
                        case "em":
                            italic = !closing;
                            EmitFormat(builder, bold, italic, underline, strike);
                            break;
                        case "u":
                            underline = !closing;
                            EmitFormat(builder, bold, italic, underline, strike);
                            break;
                        case "s":
                        case "strike":
                        case "del":
                            strike = !closing;
                            EmitFormat(builder, bold, italic, underline, strike);
                            break;
                        case "br":
                            builder.Append(@"\par ");
                            wroteParagraph = true;
                            break;
                        case "p":
                        case "div":
                        case "li":
                        case "tr":
                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                        case "blockquote":
                            if (closing && wroteParagraph)
                            {
                                builder.Append(@"\par ");
                            }

                            wroteParagraph = true;
                            break;
                    }

                    continue;
                }

                if (c == '&')
                {
                    var semi = html.IndexOf(';', index);
                    if (semi > index && semi - index <= 10)
                    {
                        var entity = html[index..(semi + 1)];
                        var decoded = WebUtility.HtmlDecode(entity);
                        if (!string.Equals(decoded, entity, StringComparison.Ordinal))
                        {
                            AppendRtfText(builder, decoded);
                            index = semi + 1;
                            continue;
                        }
                    }
                }

                AppendRtfText(builder, c.ToString());
                index++;
            }
        }

        builder.Append(@"\par}");
        return builder.ToString();
    }

    /// <summary>Converts rich-text editor output back into sanitised HTML for storage.</summary>
    public static string RtfToHtml(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return string.Empty;
        }

        var text = rtf.Trim();
        if (!text.StartsWith(@"{\rtf", StringComparison.Ordinal))
        {
            return EscapeHtml(text);
        }

        var output = new StringBuilder(text.Length);
        var stack = new Stack<Format>();
        var current = default(Format);
        var applied = default(Format);
        var depth = 0;
        var skipDepth = -1;
        var ucSkip = 1;
        var index = 0;

        while (index < text.Length)
        {
            var c = text[index];

            if (c == '{')
            {
                depth++;
                stack.Push(current);
                index++;
                continue;
            }

            if (c == '}')
            {
                depth--;
                if (stack.Count > 0)
                {
                    current = stack.Pop();
                }

                if (skipDepth >= 0 && depth < skipDepth)
                {
                    skipDepth = -1;
                }

                index++;
                continue;
            }

            if (skipDepth >= 0)
            {
                index++;
                continue;
            }

            if (c == '\\')
            {
                index++;
                if (index >= text.Length)
                {
                    break;
                }

                var symbol = text[index];

                if (!char.IsLetter(symbol))
                {
                    index++;
                    switch (symbol)
                    {
                        case '\\':
                        case '{':
                        case '}':
                            SyncFormat(output, ref applied, current);
                            AppendHtml(output, symbol);
                            break;
                        case '~':
                            SyncFormat(output, ref applied, current);
                            AppendHtml(output, '\u00A0');
                            break;
                        case '_':
                            SyncFormat(output, ref applied, current);
                            AppendHtml(output, '-');
                            break;
                        case '\'':
                            if (index + 2 <= text.Length &&
                                byte.TryParse(text.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ansi))
                            {
                                SyncFormat(output, ref applied, current);
                                AppendHtml(output, (char)ansi);
                                index += 2;
                            }

                            break;
                    }

                    continue;
                }

                var wordStart = index;
                while (index < text.Length && char.IsLetter(text[index]))
                {
                    index++;
                }

                var word = text[wordStart..index].ToLowerInvariant();

                var negative = false;
                if (index < text.Length && text[index] == '-')
                {
                    negative = true;
                    index++;
                }

                var digitsStart = index;
                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }

                var hasParameter = index > digitsStart;
                var parameter = hasParameter
                    ? int.Parse(text[digitsStart..index], CultureInfo.InvariantCulture) * (negative ? -1 : 1)
                    : 0;

                if (index < text.Length && text[index] == ' ')
                {
                    index++;
                }

                switch (word)
                {
                    case "uc":
                        ucSkip = hasParameter ? parameter : 1;
                        break;
                    case "u":
                        var code = parameter;
                        if (code < 0)
                        {
                            code += 0x10000;
                        }

                        if (code >= 0 && code <= char.MaxValue)
                        {
                            SyncFormat(output, ref applied, current);
                            AppendHtml(output, (char)code);
                        }

                        var fallback = Math.Max(0, ucSkip);
                        while (fallback > 0 && index < text.Length && text[index] is not ('{' or '}'))
                        {
                            index++;
                            fallback--;
                        }

                        break;
                    case "par":
                    case "line":
                        SyncFormat(output, ref applied, current);
                        output.Append('\n');
                        break;
                    case "tab":
                        SyncFormat(output, ref applied, current);
                        AppendHtml(output, '\t');
                        break;
                    case "plain":
                        current = default;
                        break;
                    case "b":
                        current = current with { Bold = !hasParameter || parameter != 0 };
                        break;
                    case "i":
                        current = current with { Italic = !hasParameter || parameter != 0 };
                        break;
                    case "ul":
                    case "ulnone":
                        current = current with { Underline = word == "ul" && (!hasParameter || parameter != 0) };
                        break;
                    case "strike":
                        current = current with { Strike = !hasParameter || parameter != 0 };
                        break;
                    default:
                        if (SkippedDestinations.Contains(word))
                        {
                            skipDepth = depth;
                        }

                        break;
                }

                continue;
            }

            if (c is '\r' or '\n')
            {
                index++;
                continue;
            }

            SyncFormat(output, ref applied, current);
            AppendHtml(output, c);
            index++;
        }

        SyncFormat(output, ref applied, default);
        return output.ToString().Trim();
    }

    /// <summary>Opens and closes inline tags so the emitted HTML matches the RTF run state.</summary>
    private static void SyncFormat(StringBuilder output, ref Format applied, Format target)
    {
        if (applied.Strike && !target.Strike)
        {
            output.Append("</s>");
            applied = applied with { Strike = false };
        }

        if (applied.Underline && !target.Underline)
        {
            output.Append("</u>");
            applied = applied with { Underline = false };
        }

        if (applied.Italic && !target.Italic)
        {
            output.Append("</i>");
            applied = applied with { Italic = false };
        }

        if (applied.Bold && !target.Bold)
        {
            output.Append("</b>");
            applied = applied with { Bold = false };
        }

        if (target.Bold && !applied.Bold)
        {
            output.Append("<b>");
            applied = applied with { Bold = true };
        }

        if (target.Italic && !applied.Italic)
        {
            output.Append("<i>");
            applied = applied with { Italic = true };
        }

        if (target.Underline && !applied.Underline)
        {
            output.Append("<u>");
            applied = applied with { Underline = true };
        }

        if (target.Strike && !applied.Strike)
        {
            output.Append("<s>");
            applied = applied with { Strike = true };
        }
    }

    private static void EmitFormat(StringBuilder builder, bool bold, bool italic, bool underline, bool strike)
    {
        builder.Append(bold ? @"\b " : @"\b0 ");
        builder.Append(italic ? @"\i " : @"\i0 ");
        builder.Append(underline ? @"\ul " : @"\ulnone ");
        builder.Append(strike ? @"\strike " : @"\strike0 ");
    }

    private static void AppendRtfText(StringBuilder builder, string text)
    {
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '{':
                    builder.Append(@"\{");
                    break;
                case '}':
                    builder.Append(@"\}");
                    break;
                case '\r':
                    break;
                case '\n':
                    builder.Append(@"\par ");
                    break;
                case '\t':
                    builder.Append(@"\tab ");
                    break;
                case '\u00A0':
                    builder.Append(@"\~");
                    break;
                default:
                    if (ch < 128)
                    {
                        builder.Append(ch);
                    }
                    else
                    {
                        builder.Append(@"\u").Append((int)ch).Append('?');
                    }

                    break;
            }
        }
    }

    private static void AppendHtml(StringBuilder builder, char ch)
    {
        switch (ch)
        {
            case '&':
                builder.Append("&amp;");
                break;
            case '<':
                builder.Append("&lt;");
                break;
            case '>':
                builder.Append("&gt;");
                break;
            default:
                builder.Append(ch);
                break;
        }
    }

    private static string EscapeHtml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string SanitizeUrlAttribute(Match match)
    {
        var url = match.Groups["dq"].Success ? match.Groups["dq"].Value
            : match.Groups["sq"].Success ? match.Groups["sq"].Value
            : match.Groups["bq"].Value;

        var compact = new string(url.Where(ch => !char.IsWhiteSpace(ch) && !char.IsControl(ch)).ToArray()).ToLowerInvariant();
        var separator = compact.IndexOf(':');
        if (separator > 0)
        {
            var scheme = compact[..separator];
            if (scheme is "javascript" or "vbscript")
            {
                return string.Empty;
            }

            if (scheme == "data" && !compact.StartsWith("data:image/", StringComparison.Ordinal))
            {
                return string.Empty;
            }
        }

        return match.Groups["attr"].Value + "\"" + url.Replace("\"", "&quot;", StringComparison.Ordinal) + "\"";
    }

    private readonly record struct Format(bool Bold, bool Italic, bool Underline, bool Strike);
}
