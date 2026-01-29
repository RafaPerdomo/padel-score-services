using System.Text.Json;

namespace PadelApi.DTOs.Requests;

public record FinishMatchRequest(
    string UserId,
    bool Won,
    long ExpectedVersion,
    JsonElement? FinalState,
    JsonElement? FinalStats
);
