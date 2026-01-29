using System.Text.Json;

namespace PadelApi.Models;

public class MatchState
{
    public Guid MatchId { get; set; }
    public long Version { get; set; }
    public JsonDocument StateJson { get; set; } = JsonDocument.Parse("{}");
    public DateTime UpdatedAt { get; set; }
}
