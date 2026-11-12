namespace Inspecta.Domain.Enums;

public enum UserRole
{
    Coordinator = 1,
    Inspector = 2,
    TechnicalManager = 3,
    /// <summary>The platform owner: not part of a company; sees every company.</summary>
    Admin = 4,

    /// <summary>The account a company is handed over with: manages the company's identity and its accounts, and sees no business data.</summary>
    CompanyAdmin = 5,

    /// <summary>Reserved: its rights are not decided yet, so it can sign in and change its password and nothing else.</summary>
    GeneralManager = 6,

    /// <summary>Reserved: its rights are not decided yet, so it can sign in and change its password and nothing else.</summary>
    FinancialManager = 7
}
