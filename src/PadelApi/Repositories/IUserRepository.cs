using PadelApi.Models;

namespace PadelApi.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User> UpsertAsync(string id, string? name, string? email);
    Task<bool> ExistsAsync(string id);
}
