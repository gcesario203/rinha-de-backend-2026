using AntiFraud.Core.Transaction.ValueObjects;
using AntiFraud.Core.Transaction.Entities;
using AntiFraud.Core.Transaction.DataTransferObjects;

namespace AntiFraud.Core.Transaction.Mappers;

public static class TransactionMapper
{
    public static TransactionEntity MapToEntity(TransactionRequest dto) =>
        TransactionEntity.Create(
            id: new TransactionId(dto.Id),
            payment: new Money(dto.Transaction.Amount, dto.Transaction.Installments),
            requestedAt: dto.Transaction.RequestedAt,
            customer: new CustomerProfile(dto.Customer.AvgAmount, dto.Customer.TxCount24h, dto.Customer.KnownMerchants),
            merchant: new MerchantProfile(dto.Merchant.Id, dto.Merchant.Mcc, dto.Merchant.AvgAmount),
            terminal: new TerminalContext(dto.Terminal.IsOnline, dto.Terminal.CardPresent, dto.Terminal.KmFromHome),
            lastTransaction:
            dto.LastTransaction is null ? null :
            new LastTransactionContext(dto.LastTransaction.Timestamp, dto.LastTransaction.KmFromCurrent)
        );
}