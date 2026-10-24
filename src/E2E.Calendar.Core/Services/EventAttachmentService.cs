using E2E.Calendar.Core.Abstractions;
using E2E.Calendar.Core.Models;

namespace E2E.Calendar.Core.Services;

public sealed class EventAttachmentService
{
    private readonly IAttachmentRepository _attachments;
    private readonly IClock _clock;

    public EventAttachmentService(IAttachmentRepository attachments, IClock clock)
    {
        _attachments = attachments;
        _clock = clock;
    }

    public Task<IReadOnlyList<EventAttachment>> GetAsync(
        string eventId,
        CancellationToken cancellationToken = default) =>
        _attachments.GetByEventIdAsync(eventId, cancellationToken);

    public async Task<EventAttachment> AddAsync(
        string eventId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The attachment file was not found.", filePath);
        }

        var attachment = new EventAttachment
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            FilePath = file.FullName,
            DisplayName = file.Name,
            ContentType = GetContentType(file.Extension),
            SizeBytes = file.Length,
            AddedUtc = _clock.UtcNow,
        };
        await _attachments.UpsertAsync(attachment, cancellationToken);
        return attachment;
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default) =>
        _attachments.DeleteAsync(id, cancellationToken);

    private static string? GetContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => null,
        };
}
