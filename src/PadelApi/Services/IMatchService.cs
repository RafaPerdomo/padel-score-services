using PadelApi.DTOs.Requests;
using PadelApi.DTOs.Responses;

namespace PadelApi.Services;

public interface IMatchService
{
    Task<MatchResponse> CreateMatchAsync(CreateMatchRequest request);
    Task<MatchResponse> GetActiveMatchAsync(string userId);
    Task<MatchResponse> RegisterPointAsync(Guid matchId, RegisterPointRequest request);
    Task<MatchResponse> UndoAsync(Guid matchId, UndoRequest request);
    Task<MatchResponse> UpdateStateAsync(Guid matchId, UpdateStateRequest request);
    Task FinishMatchAsync(Guid matchId, FinishMatchRequest request);
}
