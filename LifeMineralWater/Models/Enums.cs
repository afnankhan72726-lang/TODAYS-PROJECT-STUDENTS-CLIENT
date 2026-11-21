namespace LifeMineralWater.Models;

public enum AccountStatus { Active = 1, Inactive = 2, Suspended = 3 }

public enum LedgerEntryType
{
    Delivery = 1,      // filled delivered + empties received + sale + collection
    Payment = 2,       // customer money received (reduces receivable / creates advance)
    Adjustment = 3,    // admin money/bottle correction (increases receivable)
    BottleOpening = 4, // opening bottle balance
    MoneyOpening = 5   // opening money balance
}

public enum ModificationRequestType { Modify = 1, Delete = 2 }
public enum RequestStatus { Pending = 1, Approved = 2, Rejected = 3 }

public enum ExpenseCategory { Lunch = 1, Fuel = 2, Wages = 3, LoadingUnloading = 4, Other = 5 }
public enum CashDirection { In = 1, Out = 2 }
public enum CashCategory
{
    Opening = 1, Collection = 2, Expense = 3, Advance = 4,
    BankDeposit = 5, OwnerInvestment = 6, Other = 7
}
public enum PayrollStatus { Unpaid = 1, Paid = 2 }
public enum AnnouncementType { Promotion = 1, Discount = 2, SpecialOffer = 3, Announcement = 4 }
public enum SupplierEntryType { Purchase = 1, Payment = 2, Adjustment = 3 }
public enum AuditAction { Insert = 1, Update = 2, Delete = 3 }
public enum SettingType { Text = 1, Number = 2, Boolean = 3, Date = 4 }
