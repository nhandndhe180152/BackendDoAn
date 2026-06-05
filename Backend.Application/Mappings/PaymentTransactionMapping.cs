using System;
using Backend.Application.DTOs.PaymentTransactions;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PaymentTransactionMapping
{
    public static PaymentTransactionDetailDto ToDto(this PaymentTransaction entity)
    {
        return new PaymentTransactionDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Amount = entity.Amount,
            BankTransactionId = entity.BankTransactionId,
            InvoiceNumber = entity.InvoiceNumber,
            Metadata = entity.Metadata,
            Note = entity.Note,
            PaidAt = entity.PaidAt,
            PaymentCode = entity.PaymentCode,
            PaymentMethodId = entity.PaymentMethodId,
            PaymentStatusId = entity.PaymentStatusId,

        };
    }

    public static PaymentTransaction ToEntity(this CreatePaymentTransactionDto dto)
    {
        return new PaymentTransaction
        {
            CreatedDate = DateTime.Now,
            Amount = dto.Amount,
            BankTransactionId = dto.BankTransactionId,
            InvoiceNumber = dto.InvoiceNumber,
            Metadata = dto.Metadata,
            Note = dto.Note,
            PaidAt = dto.PaidAt,
            PaymentCode = dto.PaymentCode,
            PaymentMethodId = dto.PaymentMethodId,
            PaymentStatusId = dto.PaymentStatusId,
            CreatedBy = dto.CreatedBy,
        };
    }
}
