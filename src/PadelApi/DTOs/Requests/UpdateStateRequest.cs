using System.Text.Json;

namespace PadelApi.DTOs.Requests;

public record UpdateStateRequest(
    string UserId,
    long ExpectedVersion,
    JsonElement State
);
