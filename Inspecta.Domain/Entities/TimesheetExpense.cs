namespace Inspecta.Domain.Entities;

public sealed record TimesheetExpense(string Name, long AmountPiastres)
{
    public static TimesheetExpense Create(string name, long amountPiastres)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Expense name is required.", nameof(name));
        if (amountPiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(amountPiastres));

        return new TimesheetExpense(name.Trim(), amountPiastres);
    }
}
