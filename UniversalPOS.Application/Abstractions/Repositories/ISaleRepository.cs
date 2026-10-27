using System;
using System.Collections.Generic;
using System.Text;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(int id);

    Task AddAsync(Sale sale);

    Task UpdateAsync(Sale sale);
    Task RegisterSplitPaymentAsync(Sale sale);
}
