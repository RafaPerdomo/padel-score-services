using Dapper;
using Npgsql;
using PadelApi.Models;
using PadelApi.Exceptions;

namespace PadelApi.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT id, name, email, created_at, updated_at FROM users WHERE id = @Id",
            new { Id = id }
        );
    }

    public async Task<User> UpsertAsync(string id, string? name, string? email)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        try
        {
            var user = await conn.QuerySingleAsync<User>(
                """
                INSERT INTO users (id, name, email, created_at, updated_at)
                VALUES (@Id, @Name, @Email, NOW(), NOW())
                ON CONFLICT (id) DO UPDATE
                SET name = EXCLUDED.name,
                    email = EXCLUDED.email,
                    updated_at = NOW()
                RETURNING id, name, email, created_at, updated_at
                """,
                new { Id = id, Name = name, Email = email }
            );

            return user;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ConflictException("Email already exists");
        }
    }
}
