namespace PadelApi.Models;

public class Match
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; }
    public string Status { get; set; } = "LIVE";
    public bool? Won { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
