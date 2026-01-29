using PadelApi.DTOs.Requests;
using PadelApi.DTOs.Responses;
using PadelApi.Exceptions;
using PadelApi.Models;
using PadelApi.Repositories;
using System.Text.Json;

namespace PadelApi.Services;

public class MatchService : IMatchService
{
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<MatchService> _logger;

    public MatchService(IMatchRepository matchRepository, ILogger<MatchService> logger)
    {
        _matchRepository = matchRepository;
        _logger = logger;
    }

    public async Task<MatchResponse> CreateMatchAsync(CreateMatchRequest request)
    {
        var existingMatch = await _matchRepository.GetLiveMatchByUserIdAsync(request.UserId);

        if (existingMatch != null)
        {
            _logger.LogInformation("User {UserId} already has a LIVE match {MatchId}", request.UserId, existingMatch.Id);
            var existingState = await _matchRepository.GetStateAsync(existingMatch.Id);

            if (existingState == null)
            {
                throw new InvalidOperationException("Match exists but state is missing");
            }

            return new MatchResponse(
                existingMatch.Id,
                existingMatch.Status,
                existingState.Version,
                existingState.StateJson
            );
        }

        var initialState = request.InitialState ?? CreateDefaultInitialState(request);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            PlayedAt = DateTime.UtcNow,
            Status = "LIVE"
        };

        var state = new MatchState
        {
            MatchId = match.Id,
            Version = 0,
            StateJson = initialState
        };

        var startEvent = new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            Seq = 1,
            EventType = "START",
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                mode = request.Mode,
                goldenPoint = request.GoldenPoint,
                players = request.Players
            }))
        };

        var (createdMatch, createdState) = await _matchRepository.CreateMatchAsync(match, state, startEvent);

        _logger.LogInformation("Created match {MatchId} for user {UserId}", createdMatch.Id, request.UserId);

        return new MatchResponse(
            createdMatch.Id,
            createdMatch.Status,
            createdState.Version,
            createdState.StateJson
        );
    }

    public async Task<MatchResponse> GetActiveMatchAsync(string userId)
    {
        var match = await _matchRepository.GetLiveMatchByUserIdAsync(userId);

        if (match == null)
        {
            throw new NotFoundException($"No active match found for user {userId}");
        }

        var state = await _matchRepository.GetStateAsync(match.Id);

        if (state == null)
        {
            throw new InvalidOperationException("Match exists but state is missing");
        }

        return new MatchResponse(
            match.Id,
            match.Status,
            state.Version,
            state.StateJson
        );
    }

    public async Task<MatchResponse> RegisterPointAsync(Guid matchId, RegisterPointRequest request)
    {
        var match = await ValidateMatchOwnershipAndStatus(matchId, request.UserId, "LIVE");

        var eventPayload = JsonDocument.Parse(JsonSerializer.Serialize(new { winner = request.Winner }));
        var evt = new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            EventType = "POINT",
            Payload = eventPayload
        };

        await _matchRepository.InsertEventAsync(evt);

        var stateJson = request.NewState.GetRawText();
        var (success, currentState) = await _matchRepository.UpdateStateWithVersionAsync(
            matchId,
            request.ExpectedVersion,
            stateJson
        );

        if (!success)
        {
            _logger.LogWarning("Version conflict for match {MatchId}. Expected {Expected}, current {Current}",
                matchId, request.ExpectedVersion, currentState?.Version);

            throw new ConflictException("Version conflict", new
            {
                currentVersion = currentState?.Version,
                currentState = currentState?.StateJson
            });
        }

        return new MatchResponse(
            matchId,
            match.Status,
            currentState!.Version,
            currentState.StateJson
        );
    }

    public async Task<MatchResponse> UndoAsync(Guid matchId, UndoRequest request)
    {
        var match = await ValidateMatchOwnershipAndStatus(matchId, request.UserId, "LIVE");

        var evt = new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            EventType = "UNDO",
            Payload = JsonDocument.Parse("{}")
        };

        await _matchRepository.InsertEventAsync(evt);

        var stateJson = request.NewState.GetRawText();
        var (success, currentState) = await _matchRepository.UpdateStateWithVersionAsync(
            matchId,
            request.ExpectedVersion,
            stateJson
        );

        if (!success)
        {
            throw new ConflictException("Version conflict", new
            {
                currentVersion = currentState?.Version,
                currentState = currentState?.StateJson
            });
        }

        return new MatchResponse(
            matchId,
            match.Status,
            currentState!.Version,
            currentState.StateJson
        );
    }

    public async Task<MatchResponse> UpdateStateAsync(Guid matchId, UpdateStateRequest request)
    {
        var match = await ValidateMatchOwnershipAndStatus(matchId, request.UserId, "LIVE");

        var stateJson = request.State.GetRawText();
        var (success, currentState) = await _matchRepository.UpdateStateWithVersionAsync(
            matchId,
            request.ExpectedVersion,
            stateJson
        );

        if (!success)
        {
            throw new ConflictException("Version conflict", new
            {
                currentVersion = currentState?.Version,
                currentState = currentState?.StateJson
            });
        }

        return new MatchResponse(
            matchId,
            match.Status,
            currentState!.Version,
            currentState.StateJson
        );
    }

    public async Task FinishMatchAsync(Guid matchId, FinishMatchRequest request)
    {
        var match = await ValidateMatchOwnershipAndStatus(matchId, request.UserId, "LIVE");

        if (request.FinalState != null)
        {
            var stateJson = request.FinalState.Value.GetRawText();
            var (success, _) = await _matchRepository.UpdateStateWithVersionAsync(
                matchId,
                request.ExpectedVersion,
                stateJson
            );

            if (!success)
            {
                throw new ConflictException("Version conflict when updating final state");
            }
        }

        var eventPayload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            won = request.Won,
            finalStats = request.FinalStats
        }));

        var evt = new MatchEvent
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            EventType = "MATCH_END",
            Payload = eventPayload
        };

        await _matchRepository.InsertEventAsync(evt);
        await _matchRepository.UpdateMatchStatusAsync(matchId, "FINISHED", request.Won);

        _logger.LogInformation("Match {MatchId} finished. Won: {Won}", matchId, request.Won);
    }

    public async Task DeleteActiveMatchAsync(string userId)
    {
        var match = await _matchRepository.GetLiveMatchByUserIdAsync(userId);

        if (match == null)
        {
            // Idempotent: if no active match, we consider it "deleted" successfully
            return;
        }

        await _matchRepository.UpdateMatchStatusAsync(match.Id, "ABANDONED", null);
        _logger.LogInformation("Match {MatchId} abandoned/deleted by request for user {UserId}", match.Id, userId);
    }

    private async Task<Match> ValidateMatchOwnershipAndStatus(Guid matchId, string userId, string requiredStatus)
    {
        _logger.LogInformation("🔍 Validating match ownership - MatchId: {MatchId}, RequestUserId: '{UserId}'", matchId, userId);

        var match = await _matchRepository.GetByIdAsync(matchId);

        if (match == null)
        {
            _logger.LogWarning("❌ Match {MatchId} not found", matchId);
            throw new NotFoundException($"Match {matchId} not found");
        }

        _logger.LogInformation("📋 Match found - MatchUserId: '{MatchUserId}', Status: {Status}", match.UserId, match.Status);
        _logger.LogInformation("🔑 Comparing - Request: '{RequestUserId}' vs Match: '{MatchUserId}' | Equal: {IsEqual}",
            userId, match.UserId, match.UserId == userId);

        if (match.UserId != userId)
        {
            _logger.LogError("❌ Ownership validation failed - Match {MatchId} belongs to '{MatchUserId}' but request from '{RequestUserId}'",
                matchId, match.UserId, userId);
            throw new ForbiddenException($"Match {matchId} does not belong to user {userId}");
        }

        if (match.Status != requiredStatus)
        {
            _logger.LogWarning("❌ Status validation failed - Match status is {Status}, expected {RequiredStatus}",
                match.Status, requiredStatus);
            throw new ConflictException($"Match status is {match.Status}, expected {requiredStatus}");
        }

        _logger.LogInformation("✅ Validation passed for match {MatchId}", matchId);
        return match;
    }

    private JsonDocument CreateDefaultInitialState(CreateMatchRequest request)
    {
        var defaultState = new
        {
            mode = request.Mode,
            goldenPoint = request.GoldenPoint,
            players = request.Players,
            score = new
            {
                teamA = new { games = 0, sets = 0 },
                teamB = new { games = 0, sets = 0 }
            },
            currentSet = 1,
            history = new object[] { }
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(defaultState));
    }
}
