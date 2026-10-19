namespace E2E.Calendar.Core.Abstractions;

public interface IEventQueryCache
{
    event EventHandler? Invalidated;

    void Invalidate();
}
