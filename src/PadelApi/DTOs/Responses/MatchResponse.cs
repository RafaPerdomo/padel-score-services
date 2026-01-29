using System.Text.Json;

namespace PadelApi.DTOs.Responses;

public record MatchResponse(
    Guid MatchId,
    string Status,
    long Version,
    JsonDocument State,
    bool? Won = null
);
