using System.Text.Json;

namespace PadelApi.DTOs.Requests;

public record UndoRequest(
    string UserId,
    long ExpectedVersion,
    JsonElement NewState
);
