using System.Text;
using System.Text.RegularExpressions;
using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class ScheduleExportService
{
    private readonly IEventRepository _events;

    public ScheduleExportService(IEventRepository events)
    {
        _events = events;
    }

    public async Task<ScheduleExportDocument> BuildAsync(
        ScheduleExportView view,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc));
        }

        var events = await _events.GetInRangeAsync(null, fromUtc, toUtc, cancellationToken);
        return new ScheduleExportDocument
        {
            View = view,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Events = events.Where(item => !item.Deleted).OrderBy(item => item.StartUtc).ToArray(),
        };
    }

    public string ToHtml(ScheduleExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var title = $"{document.View} schedule";
        var rows = document.Events.Count == 0
            ? "<tr><td colspan=\"3\">No events</td></tr>"
            : string.Join(
                Environment.NewLine,
                document.Events.Select(item =>
                    $"<tr><td>{Escape(item.StartUtc.ToLocalTime().ToString("g"))}</td><td>{Escape(item.Title)}</td><td>{Escape(item.Location ?? string.Empty)}</td></tr>"));

        return $$"""
            <!doctype html>
            <html><head><meta charset="utf-8"><title>{{Escape(title)}}</title>
            <style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px}table{border-collapse:collapse;width:100%}th,td{border-bottom:1px solid #ccc;padding:8px;text-align:left}h1{font-size:24px}</style>
            </head><body><h1>{{Escape(title)}}</h1>
            <table><thead><tr><th>Time</th><th>Event</th><th>Location</th></tr></thead><tbody>{{rows}}</tbody></table>
            </body></html>
            """;
    }

    public byte[] ToPdf(ScheduleExportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var lines = new List<string> { $"{document.View} schedule" };
        lines.AddRange(document.Events.Count == 0
            ? ["No events"]
            : document.Events.Select(item =>
                $"{item.StartUtc.ToLocalTime():g}  {item.Title}  {item.Location}".TrimEnd()));

        var content = new StringBuilder("BT\n/F1 10 Tf\n50 760 Td\n");
        foreach (var line in lines)
        {
            content.Append('(').Append(EscapePdf(line)).Append(") Tj\n0 -16 Td\n");
        }

        content.Append("ET");
        return BuildPdf(content.ToString());
    }

    private static byte[] BuildPdf(string content)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("%PDF-1.4\n");
        writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            writer.Flush();
        }

        var xref = stream.Position;
        writer.Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            writer.Write($"{offset:D10} 00000 n \n");
        }

        writer.Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        writer.Flush();
        return stream.ToArray();
    }

    private static string Escape(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private static string EscapePdf(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
