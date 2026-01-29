using System.Text.Json;

namespace PadelApi.DTOs.Requests;

public record CreateMatchRequest(
    string UserId,
    string Mode,
    bool GoldenPoint,
    List<string> Players,
    JsonDocument? InitialState
);
