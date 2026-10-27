
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Tests;

public class FakeSaleRepository : ISaleRepository
{
    private readonly Dictionary<int, Sale> _sales = new();
    private int _nextId = 1;

    public int UpdateCallCount { get; private set; }

    public Sale? SavedSale { get; private set; }

    public Task<Sale?> GetByIdAsync(int id)
    {
        _sales.TryGetValue(id, out Sale? sale);
        return Task.FromResult(sale);
    }

    public Task AddAsync(Sale sale)
    {
        // Simulate a database-generated ID for testing.
        typeof(Sale).GetProperty(nameof(Sale.Id))!.SetValue(sale, _nextId++);

        _sales[sale.Id] = sale;
        SavedSale = sale;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Sale sale)
    {
        if (!_sales.ContainsKey(sale.Id))
            throw new InvalidOperationException("Sale not found.");

        _sales[sale.Id] = sale;
        SavedSale = sale;
        UpdateCallCount++;

        return Task.CompletedTask;
    }

    public Task RegisterSplitPaymentAsync(Sale sale)
    {
        _sales[sale.Id] = sale;
        SavedSale = sale;

        return Task.CompletedTask;
    }
}
