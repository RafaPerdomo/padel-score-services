using System.Text.Json;

namespace PadelApi.Models;

public class MatchEvent
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public long Seq { get; set; }
    public string EventType { get; set; } = string.Empty;
    public JsonDocument Payload { get; set; } = JsonDocument.Parse("{}");
    public DateTime CreatedAt { get; set; }
}
