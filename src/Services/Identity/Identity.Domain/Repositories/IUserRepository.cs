using Identity.Domain.Entities;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Kernel.Enums.Identity;

namespace Identity.Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    IQueryable<User> GetUsersByRole(UserRole role);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> UpdateRefreshTokenAsync(int userId, string refreshToken, DateTime expiryTime, CancellationToken cancellationToken = default);
    Task<bool> RevokeRefreshTokenAsync(int userId, CancellationToken cancellationToken = default);
}