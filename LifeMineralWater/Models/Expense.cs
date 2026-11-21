using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>
/// Business or personal expense recorded by a salesman or by the owner.
/// Every expense also produces a cash-book outflow in the same transaction.
/// </summary>
public class Expense
{
    public int Id { get; set; }

    [Required, StringLength(300)]
    public string Description { get; set; } = string.Empty;

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal Amount { get; set; }

    [Required]
    public int RecordedByUserId { get; set; }

    public AppUser RecordedBy { get; set; } = null!;

    public int? SalesmanId { get; set; }

    public Salesman? Salesman { get; set; }

    /// <summary>Personal expenses are tracked but excluded from profit and loss.</summary>
    public bool IsBusinessExpense { get; set; } = true;

    public DateTime ExpenseDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
