using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models.ViewModels;

public class DeliveryInput
{
    [Required] public int CustomerId { get; set; }
    [Required] public int ProductId { get; set; }
    [Required] public int BottleTypeId { get; set; }

    [Range(0, 100000, ErrorMessage = "Filled bottles cannot be negative.")]
    public int FilledDelivered { get; set; }

    [Range(0, 100000, ErrorMessage = "Empty bottles cannot be negative.")]
    public int EmptyReceived { get; set; }

    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Amount collected cannot be negative.")]
    public decimal AmountReceived { get; set; }

    [StringLength(300)] public string? Description { get; set; }
    public DateTime? EntryDate { get; set; }
}

public class PaymentInput
{
    [Required] public int CustomerId { get; set; }

    [Range(typeof(decimal), "0.01", "10000000", ErrorMessage = "Enter a positive amount.")]
    public decimal AmountReceived { get; set; }

    [StringLength(300)] public string? Description { get; set; }
    public DateTime? EntryDate { get; set; }
}

public class ExpenseInput
{
    [Required, StringLength(300)] public string Description { get; set; } = string.Empty;
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public bool IsBusinessExpense { get; set; } = true;
}

public class AdvanceInput
{
    [Required] public int StaffId { get; set; }
    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime? AdvanceDate { get; set; }
    [StringLength(300)] public string? Note { get; set; }
}

public class AdjustmentInput
{
    [Required] public int CustomerId { get; set; }
    public int BottleCorrection { get; set; }

    [Range(typeof(decimal), "0", "10000000")]
    public decimal AmountAdjusted { get; set; }

    [Range(typeof(decimal), "0", "10000000")]
    public decimal AmountReceived { get; set; }

    [Required, StringLength(300)] public string Description { get; set; } = string.Empty;
}

public class ChangeRequestInput
{
    [Required] public int LedgerEntryId { get; set; }
    public ModificationRequestType RequestType { get; set; } = ModificationRequestType.Modify;

    [Range(0, 100000)] public int RequestedFilled { get; set; }
    [Range(0, 100000)] public int RequestedEmpty { get; set; }

    [Range(typeof(decimal), "0", "10000000")] public decimal RequestedAmountReceived { get; set; }

    [Range(typeof(decimal), "0", "10000000")] public decimal RequestedRate { get; set; }

    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}
