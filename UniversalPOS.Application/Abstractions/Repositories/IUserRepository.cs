
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);

    Task<User?> GetByUserNameAsync(string userName);

    Task UpdateAsync(User user);
}
