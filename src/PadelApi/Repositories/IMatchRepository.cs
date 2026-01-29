using PadelApi.Models;

namespace PadelApi.Repositories;

public interface IMatchRepository
{
    Task<Match?> GetLiveMatchByUserIdAsync(string userId);
    Task<Match?> GetByIdAsync(Guid matchId);
    Task<(Match Match, MatchState State)> CreateMatchAsync(Match match, MatchState state, MatchEvent? startEvent);
    Task<MatchState?> GetStateAsync(Guid matchId);
    Task<(bool Success, MatchState? CurrentState)> UpdateStateWithVersionAsync(
        Guid matchId,
        long expectedVersion,
        string stateJson
    );
    Task<long> InsertEventAsync(MatchEvent evt);
    Task UpdateMatchStatusAsync(Guid matchId, string status, bool? won);
}
