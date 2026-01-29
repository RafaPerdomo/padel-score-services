namespace PadelApi.DTOs.Responses;

public record UserResponse(
    string Id,
    string? Name,
    string? Email
);
