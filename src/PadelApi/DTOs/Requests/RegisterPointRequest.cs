using System.Text.Json;

namespace PadelApi.DTOs.Requests;

public record RegisterPointRequest(
    string UserId,
    string Winner,
    long ExpectedVersion,
    JsonElement NewState
);
