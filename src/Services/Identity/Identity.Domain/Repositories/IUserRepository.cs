using Identity.Domain.Entities;
using IhsanDev.Shared.Infrastructure.Persistence;

namespace Identity.Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> UpdateRefreshTokenAsync(int userId, string refreshToken, DateTime expiryTime, CancellationToken cancellationToken = default);
    Task<bool> RevokeRefreshTokenAsync(int userId, CancellationToken cancellationToken = default);
    IQueryable<User> GetUsersByRoleName(string roleName);
}