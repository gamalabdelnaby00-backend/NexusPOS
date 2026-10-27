using System;
using System.Collections.Generic;
using System.Text;
using UniversalPOS.Application.Abstractions.Repositories;

namespace UniversalPOS.Application.Sales;

public class CancelSaleService
{
    private readonly ISaleRepository _saleRepository;

    public CancelSaleService(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository;
    }

    public async Task ExecuteAsync(int saleId)
    {
        var sale = await _saleRepository.GetByIdAsync(saleId);

        if (sale is null)
            throw new InvalidOperationException("Sale not found.");

        sale.Cancel();

        await _saleRepository.UpdateAsync(sale);
    }
}

