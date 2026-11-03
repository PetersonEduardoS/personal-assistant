namespace PersonalAssistant.Core.Messaging;

/// <summary>
/// Remembers recently processed message IDs, because Meta may deliver the same webhook more than once.
/// Only the most recent IDs are kept, so memory use stays small.
/// </summary>
public sealed class ProcessedMessageTracker(int capacity = 1000)
{
    private readonly int _capacity = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

    private readonly Lock _lock = new();
    private readonly HashSet<string> _messageIds = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();

    /// <summary>
    /// Returns true the first time a message ID is seen, and false for repeats.
    /// </summary>
    public bool TryMarkAsProcessed(string messageId)
    {
        lock (_lock)
        {
            if (!_messageIds.Add(messageId))
            {
                return false;
            }

            _order.Enqueue(messageId);

            if (_order.Count > _capacity)
            {
                _messageIds.Remove(_order.Dequeue());
            }

            return true;
        }
    }
}
