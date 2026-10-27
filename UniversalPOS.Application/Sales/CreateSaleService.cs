using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.Sales
{
    public class CreateSaleService
    {
        public const string ShiftRequiredMessage =
            "You must sign in and open a cash shift before creating a sale.";

        private readonly ISaleRepository _saleRepository;
        private readonly CurrentUserSession _session;
        private readonly ICashShiftRepository? _shiftRepository;
        private readonly PermissionChecker? _permissions;

        public CreateSaleService(
            ISaleRepository saleRepository,
            CurrentUserSession session,
            ICashShiftRepository? shiftRepository = null,
            PermissionChecker? permissions = null)
        {
            _saleRepository = saleRepository;
            _session = session;
            _shiftRepository = shiftRepository;
            _permissions = permissions;
        }

        public async Task<int> ExecuteAsync()
        {
            // Users with Sales.SellWithoutShift (Admin) may sell with no shift and no drawer.
            if (_session.IsAuthenticated && _session.UserId is not null &&
                (_session.CashShiftId is null || _session.TerminalId is null) &&
                _permissions?.HasPermission(PermissionCodes.Sales.SellWithoutShift) == true)
            {
                var noShiftSale = new Sale();
                noShiftSale.AssignContextWithoutShift(_session.UserId.Value, Guid.NewGuid());
                await _saleRepository.AddAsync(noShiftSale);
                return noShiftSale.Id;
            }

            if (!_session.IsAuthenticated ||
                _session.UserId is null ||
                _session.CashShiftId is null ||
                _session.TerminalId is null)
            {
                throw ServiceErrors.Create(ServiceErrors.ShiftRequired, ShiftRequiredMessage);
            }

            // Verify the shift against the database: it must exist, be open, and belong to this user.
            if (_shiftRepository is not null)
            {
                var shift = await _shiftRepository.GetByIdAsync(_session.CashShiftId.Value);

                if (shift is null || shift.Status != CashShiftStatus.Open)
                    throw ServiceErrors.Create(ServiceErrors.ShiftNotOpen,
                        "The cash shift is not open.");

                if (shift.UserId != _session.UserId.Value)
                    throw ServiceErrors.Create(ServiceErrors.ShiftNotOwned,
                        "The cash shift belongs to a different user.");

                if (shift.TerminalId != _session.TerminalId.Value)
                    throw ServiceErrors.Create(ServiceErrors.ShiftNotOpen,
                        "The cash shift belongs to a different terminal.");
            }

            var sale = new Sale();
            sale.AssignContext(
                _session.UserId.Value,
                _session.CashShiftId.Value,
                _session.TerminalId.Value,
                Guid.NewGuid());

            await _saleRepository.AddAsync(sale);
            return sale.Id;
        }
    }
}
