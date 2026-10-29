namespace Inspecta.Application.Pdf;

/// <summary>Fixed values of the quotation that are not entered per quotation.</summary>
public static class QuotationDefaults
{
    public const int VatRatePercent = 14;
    public const string UnitOfMeasure = "AU";
    public const string PreparedByTitle = "Coordinator";

    /// <summary>
    /// Notes every new quotation starts with. The closing note about the validity period is added automatically
    /// when the PDF is generated, so it always matches the validity of the quotation.
    /// </summary>
    public static readonly IReadOnlyList<string> StandardNotes =
    [
        "Pricing excludes VAT, per RFQ instructions.",
        "Payment terms: 90 days from invoice date, as specified in the RFQ.",
        "Any additional access equipment (scaffolding, cherry picker, tank cleaning/gas-freeing for internal access) required beyond standard site access is excluded and will be quoted separately if identified during the site visit.",
        "Cancellation Policy: If any agreed inspection day is canceled with less than three (3) working days' prior notice, or if the inspection cannot be performed for reasons beyond Integrity's control, the client will be charged one full day fee.",
        "Commencement of Work: Work will commence only after issuance of the Purchase Order (PO) or formal written confirmation, agreement, and receipt of the advance payment as stated in the payment conditions.",
        "Man-Day Rate: Applicable for one inspector and up to eight (8) working hours per day, including a one-hour break.",
        "Overtime: Overtime will be charged on a prorated basis starting from the 9th working hour.",
        "Standby Rate: A standby rate of 75% of the daily rate will apply when inspectors are present but unable to perform work due to reasons beyond Integrity's control.",
        "Holiday / Weekend Work: Inspections conducted on Fridays or national holidays will be charged at 1.5 times the standard hourly rate.",
        "Please refer to the above reference number in all future correspondence.",
        "All other conditions are subject to Integrity's General Conditions of Services."
    ];

    public static string ValidityNote(int days) =>
        $"This quotation is valid for {days} days from the date of this document's issuance.";
}
