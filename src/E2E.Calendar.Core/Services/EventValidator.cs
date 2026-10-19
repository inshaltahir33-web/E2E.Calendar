namespace E2E.Calendar.Core.Services;

public static class EventValidator
{
    public static IReadOnlyList<string> Validate(
        string? title,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? url = null,
        string? meetingUrl = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("The event needs a title.");
        }

        if (endUtc < startUtc)
        {
            errors.Add("The event cannot end before it starts.");
        }

        if (!string.IsNullOrWhiteSpace(url) && !UrlPolicy.IsAllowed(url))
        {
            errors.Add("The link must be an http or https address.");
        }

        if (!string.IsNullOrWhiteSpace(meetingUrl) && !UrlPolicy.IsAllowed(meetingUrl))
        {
            errors.Add("The meeting link must be an http or https address.");
        }

        return errors;
    }
}
