using FluentValidation;
using LifeMineralWater.Models.ViewModels;

namespace LifeMineralWater.Services;

public sealed class DeliveryInputValidator : AbstractValidator<DeliveryInput>
{
    public DeliveryInputValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.BottleTypeId).GreaterThan(0);
        RuleFor(x => x.FilledDelivered).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.EmptyReceived).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.AmountReceived).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.FilledDelivered > 0 || x.EmptyReceived > 0)
            .WithMessage("At least one filled or empty bottle quantity is required.")
            .WithName("FilledDelivered");
        RuleFor(x => x.EntryDate).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .When(x => x.EntryDate.HasValue).WithMessage("Entry date cannot be in the future.");
    }
}

public sealed class PaymentInputValidator : AbstractValidator<PaymentInput>
{
    public PaymentInputValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AmountReceived).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.EntryDate).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .When(x => x.EntryDate.HasValue);
    }
}

public sealed class ExpenseInputValidator : AbstractValidator<ExpenseInput>
{
    public ExpenseInputValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class AdvanceInputValidator : AbstractValidator<AdvanceInput>
{
    public AdvanceInputValidator()
    {
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class AdjustmentInputValidator : AbstractValidator<AdjustmentInput>
{
    public AdjustmentInputValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(5);
        RuleFor(x => x).Must(x => x.BottleCorrection != 0 || x.AmountAdjusted > 0 || x.AmountReceived > 0)
            .WithMessage("Provide a bottle or money correction.");
        RuleFor(x => x.AmountAdjusted).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AmountReceived).GreaterThanOrEqualTo(0);
    }
}

public sealed class ChangeRequestInputValidator : AbstractValidator<ChangeRequestInput>
{
    public ChangeRequestInputValidator()
    {
        RuleFor(x => x.LedgerEntryId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(500);
        RuleFor(x => x.RequestedFilled).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RequestedEmpty).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RequestedAmountReceived).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RequestedRate).GreaterThanOrEqualTo(0);
    }
}

public sealed class LoginViewModelValidator : AbstractValidator<LoginViewModel>
{
    public LoginViewModelValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
