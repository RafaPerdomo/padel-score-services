namespace PadelApi.DTOs.Responses;

public record ErrorResponse(
    string Error,
    object? Details = null
);
