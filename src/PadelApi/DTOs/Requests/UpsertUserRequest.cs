using System.Text.Json;

namespace PadelApi.DTOs.Requests;

public record UpsertUserRequest(
    string? Name,
    string? Email
);
