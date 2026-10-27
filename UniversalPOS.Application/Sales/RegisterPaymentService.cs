using System;
using System.Threading.Tasks;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Sales;

public class RegisterPaymentService
{
    private readonly ISaleRepository _saleRepository;

    public RegisterPaymentService(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository;
    }

    public async Task ExecuteAsync(int saleId,decimal amount,PaymentMethod method,string? walletProvider = null)
    {
        var sale = await _saleRepository.GetByIdAsync(saleId);

        if (sale is null)
            throw new InvalidOperationException("Sale not found.");

        var payment = new Payment(amount, method, walletProvider);

        sale.AddPayment(payment);

        await _saleRepository.UpdateAsync(sale);
    }

    public async Task ExecuteSplitPaymentAsync(int saleId,decimal firstAmount,PaymentMethod firstMethod,
        decimal secondAmount,PaymentMethod secondMethod,string? firstWalletProvider = null,string? secondWalletProvider = null)
    {
        if (firstMethod == secondMethod)
            throw new InvalidOperationException("Payment methods must be different.");

        var sale = await _saleRepository.GetByIdAsync(saleId);

        if (sale is null)
            throw new InvalidOperationException("Sale not found.");

        var firstPayment = new Payment(firstAmount, firstMethod, firstWalletProvider);

        var secondPayment = new Payment(secondAmount, secondMethod, secondWalletProvider);

        sale.AddPayment(firstPayment);
        sale.AddPayment(secondPayment);

        await _saleRepository.RegisterSplitPaymentAsync(sale);
    }
}
