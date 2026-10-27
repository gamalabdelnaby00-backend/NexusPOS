using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Tests;

public sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = new();

    public Task AddAsync(AuditLog entry)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
