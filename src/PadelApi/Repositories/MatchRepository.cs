using Dapper;
using Npgsql;
using PadelApi.Models;
using System.Text.Json;

namespace PadelApi.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly string _connectionString;

    public MatchRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    public async Task<Match?> GetLiveMatchByUserIdAsync(string userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Match>(
            "SELECT id, user_id AS UserId, played_at AS PlayedAt, status::text AS Status, won, created_at AS CreatedAt, updated_at AS UpdatedAt FROM matches WHERE user_id = @UserId AND status = 'LIVE'",
            new { UserId = userId }
        );
    }

    public async Task<Match?> GetByIdAsync(Guid matchId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Match>(
            "SELECT id, user_id AS UserId, played_at AS PlayedAt, status::text AS Status, won, created_at AS CreatedAt, updated_at AS UpdatedAt FROM matches WHERE id = @Id",
            new { Id = matchId }
        );
    }

    public async Task<(Match Match, MatchState State)> CreateMatchAsync(
        Match match,
        MatchState state,
        MatchEvent? startEvent)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var createdMatch = await conn.QuerySingleAsync<Match>(
                """
                INSERT INTO matches (id, user_id, played_at, status, created_at, updated_at)
                VALUES (@Id, @UserId, @PlayedAt, @Status::match_status, NOW(), NOW())
                RETURNING id, user_id AS UserId, played_at AS PlayedAt, status::text AS Status, won, created_at AS CreatedAt, updated_at AS UpdatedAt
                """,
                new { match.Id, match.UserId, match.PlayedAt, match.Status },
                tx
            );

            var stateJsonString = state.StateJson.RootElement.GetRawText();

            var stateResult = await conn.QuerySingleAsync<(Guid match_id, long version, string state_json, DateTime updated_at)>(
                """
                INSERT INTO match_state (match_id, version, state_json, updated_at)
                VALUES (@MatchId, @Version, @StateJson::jsonb, NOW())
                RETURNING match_id, version, state_json::text, updated_at
                """,
                new { state.MatchId, state.Version, StateJson = stateJsonString },
                tx
            );

            var createdState = new MatchState
            {
                MatchId = stateResult.match_id,
                Version = stateResult.version,
                StateJson = JsonDocument.Parse(stateResult.state_json),
                UpdatedAt = stateResult.updated_at
            };

            if (startEvent != null)
            {
                var payloadString = startEvent.Payload.RootElement.GetRawText();

                await conn.ExecuteAsync(
                    """
                    INSERT INTO match_events (id, match_id, seq, event_type, payload, created_at)
                    VALUES (@Id, @MatchId, @Seq, @EventType, @Payload::jsonb, NOW())
                    """,
                    new { startEvent.Id, startEvent.MatchId, startEvent.Seq, startEvent.EventType, Payload = payloadString },
                    tx
                );
            }

            await tx.CommitAsync();
            return (createdMatch, createdState);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<MatchState?> GetStateAsync(Guid matchId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var result = await conn.QuerySingleOrDefaultAsync<(Guid match_id, long version, string state_json, DateTime updated_at)?>(
            "SELECT match_id, version, state_json::text, updated_at FROM match_state WHERE match_id = @MatchId",
            new { MatchId = matchId }
        );

        if (result == null)
        {
            return null;
        }

        return new MatchState
        {
            MatchId = result.Value.match_id,
            Version = result.Value.version,
            StateJson = JsonDocument.Parse(result.Value.state_json),
            UpdatedAt = result.Value.updated_at
        };
    }

    public async Task<(bool Success, MatchState? CurrentState)> UpdateStateWithVersionAsync(
        Guid matchId,
        long expectedVersion,
        string stateJson)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rowsAffected = await conn.ExecuteAsync(
            """
            UPDATE match_state
            SET version = version + 1,
                state_json = @StateJson::jsonb,
                updated_at = NOW()
            WHERE match_id = @MatchId AND version = @ExpectedVersion
            """,
            new { MatchId = matchId, ExpectedVersion = expectedVersion, StateJson = stateJson }
        );

        if (rowsAffected == 0)
        {
            var currentState = await GetStateAsync(matchId);
            return (false, currentState);
        }

        var updatedState = await GetStateAsync(matchId);
        return (true, updatedState);
    }

    public async Task<long> InsertEventAsync(MatchEvent evt)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var maxSeq = await conn.QuerySingleOrDefaultAsync<long?>(
            "SELECT MAX(seq) FROM match_events WHERE match_id = @MatchId",
            new { evt.MatchId }
        );

        var nextSeq = (maxSeq ?? 0) + 1;
        var payloadString = evt.Payload.RootElement.GetRawText();

        await conn.ExecuteAsync(
            """
            INSERT INTO match_events (id, match_id, seq, event_type, payload, created_at)
            VALUES (@Id, @MatchId, @Seq, @EventType, @Payload::jsonb, NOW())
            """,
            new { evt.Id, evt.MatchId, Seq = nextSeq, evt.EventType, Payload = payloadString }
        );

        return nextSeq;
    }

    public async Task UpdateMatchStatusAsync(Guid matchId, string status, bool? won)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        await conn.ExecuteAsync(
            """
            UPDATE matches
            SET status = @Status::match_status,
                won = @Won,
                updated_at = NOW()
            WHERE id = @MatchId
            """,
            new { MatchId = matchId, Status = status, Won = won }
        );
    }
}
