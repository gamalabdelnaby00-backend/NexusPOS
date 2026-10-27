using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface IAuditLogRepository
{
    /// <summary>Adds the entry to the current unit of work; it is persisted by the next SaveChanges.</summary>
    Task AddAsync(AuditLog entry);
}
