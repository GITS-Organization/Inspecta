using Inspecta.Application.Pdf.Models;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// Company details and fixed texts used when a quotation carries no company profile of its own. They are the texts
/// of the reference quotation (Integrity International for Inspection); a per-company profile can replace them later.
/// </summary>
public static class DefaultCompanyProfile
{
    public static readonly QuotationCompanyProfile Instance = new(
        Name: "Integrity International for Inspection",
        Address: "21 Nakib Sharif Ramzy st, Smouha, Alexandria, Egypt",
        Phones: ["+20 100 091 4066", "+20 155 325 6481"],
        Email: "info@integrity-intl.com",
        Website: "www.integrity-intl.com",
        AboutUs:
            "**INTEGRITY** (Environmental & Industrial Services) is a leading provider of testing, inspection, certification, maintenance, and consulting services. With years of experience and a team of dedicated experts, we specialize in ensuring compliance with international standards, enhancing workplace safety, and driving sustainable business practices. Our multidisciplinary approach caters to industries such as manufacturing, energy, construction, and more, delivering tailored solutions to meet unique client needs.",
        Vision:
            "To be the most trusted partner for industries worldwide, driving innovation and sustainability while ensuring safety, compliance, and operational excellence. We aim to create a positive impact on the environment and society through our expertise and commitment to quality.",
        Services:
        [
            new QuotationServiceSection(
                "4-1 Industrial Services",
                "Service",
                "Scope",
                Numbered: true,
                [
                    new QuotationServiceGroup(null,
                    [
                        new("Third-Party Quality Services", "QA/QC during fabrication, painting, erection, assembly, and dismantling."),
                        new("Calibration Services", "Precise calibration for tanks, silos, and industrial equipment."),
                        new("Failure & Corrosion Analysis", "Comprehensive failure investigation and coupon analysis."),
                        new("Supply Chain Services", "Supplier evaluation, third-party inspections, progress monitoring, and pre-shipment checks."),
                        new("Lifting Equipment Inspections & Training", "Inspection and certification for lifting gears, cranes, forklifts, scaffolding, and more."),
                        new("Non-Destructive Testing (NDT)", "Ultrasonic, magnetic particle, dye penetrant, radiographic, and thermal imaging."),
                        new("Advanced NDT Testing", "Phased array, long-range ultrasonic testing (LRUT), pulsed eddy current (PEC), etc."),
                        new("Chemical & Mechanical Testing", "Material testing for steel and polymer products."),
                        new("Factory Audits & Inspections", "Detailed assessments for quality assurance and compliance."),
                        new("Energy Efficiency Audits", "Optimizing energy performance to reduce costs and emissions."),
                        new("Mechanical, Electrical, and Civil Works Supervision", "Comprehensive oversight to ensure project safety and quality."),
                        new("Mechanical and Asset Integrity", "Safeguarding operations through systematic integrity assessments."),
                        new("Product Certification", "CE marking and international certifications for various products."),
                        new("Material Testing", "Testing of material properties, composition, and quality."),
                        new("Health & Safety (Process Safety Study)", "Comprehensive evaluation of industrial processes to identify hazards, assess risks, and implement process safety management measures."),
                        new("Mechanical and Electrical Maintenance", "Preventive and corrective maintenance of mechanical and electrical equipment.")
                    ])
                ]),
            new QuotationServiceSection(
                "4-2 Environmental Services",
                "Service / Activity",
                "Description / Scope",
                Numbered: false,
                [
                    new QuotationServiceGroup("ENVIRONMENTAL SERVICES",
                    [
                        new("Air Quality Monitoring", "Ambient air sampling, stack sampling, dust and gas analysis."),
                        new("Water & Wastewater Sampling & Testing", "Chemical, microbiological, and heavy metals testing."),
                        new("Soil & Sediment Sampling", "Contamination assessment, heavy metals, hydrocarbons."),
                        new("Biodiversity & Ecological Surveys", "Flora, fauna, and ecosystem studies."),
                        new("Noise & Vibration Monitoring", "Workplace and environmental monitoring."),
                        new("Environmental Auditing", "Compliance audits, impact assessments, and reporting.")
                    ]),
                    new QuotationServiceGroup("INDUSTRIAL HYGIENE",
                    [
                        new("Occupational Health Assessment", "Exposure monitoring, ergonomics, workplace hazards."),
                        new("Chemical & Biological Hazard Assessment", "Identification and measurement of workplace hazards."),
                        new("Asbestos Surveys", "Inspection, sampling, and risk assessment."),
                        new("Health & Safety Studies", "Process safety, risk assessment, mitigation planning."),
                        new("Industrial Hygiene Auditing", "Compliance checks and best practices.")
                    ]),
                    new QuotationServiceGroup("LABORATORY SERVICES",
                    [
                        new("Chemical Analysis", "Testing for metals, organics, pollutants."),
                        new("Microbiological Analysis", "Water, soil, and air samples for bacteria, fungi."),
                        new("Material Testing", "Corrosion, composition, quality control."),
                        new("Environmental Sample Analysis", "Lab testing for air, water, soil, and waste.")
                    ]),
                    new QuotationServiceGroup("CLIMATE CHANGE & SUSTAINABILITY SERVICES",
                    [
                        new("Carbon Footprint Assessment", "GHG emissions measurement."),
                        new("Carbon Accounting & Reporting", "ISO 14064, GHG Protocol, CDP Reporting."),
                        new("Climate Risk & Resilience Assessment", "Operational and supply chain risks."),
                        new("Net Zero Strategy & Roadmap", "Emissions reduction planning."),
                        new("Carbon Offset & Credit Solutions", "Offset project management."),
                        new("Sustainability Strategy Development", "ESG strategy and policies."),
                        new("Sustainability Reporting & Disclosure", "GRI, SASB, TCFD reports."),
                        new("Circular Economy & Resource Efficiency", "Waste reduction and recycling."),
                        new("Renewable Energy & Energy Efficiency", "Renewable energy projects."),
                        new("Sustainable Procurement & Supply Chain", "Supplier ESG evaluation.")
                    ])
                ])
        ],
        Terms:
        [
            new QuotationTermsSection("7.1 Invoicing and Payment",
            [
                "INTEGRITY will issue and submit an invoice upon completion of the job.",
                "The Client agrees to pay 100 % against the reports."
            ]),
            new QuotationTermsSection("7.2 Offer Validity",
            [
                "This proposal is made in good faith and remains valid for **{days} days** from the date of issue."
            ]),
            new QuotationTermsSection("7.3 Payment details",
            [
                "Payments can be made via cash or check.",
                "Bank details will be provided upon request."
            ]),
            new QuotationTermsSection("7.4 Bank Account", [], ShowBankAccounts: true),
            new QuotationTermsSection("7.5 INTEGRITY Obligations",
            [
                "Upon request, INTEGRITY shall mobilize the spare parts to the client site.",
                "All tasks will be executed in compliance with INTEGRITY **QHSE rules and regulations**, as well as safety practices and procedures."
            ]),
            new QuotationTermsSection("7.6 Client Obligations",
            [
                "The Client is responsible for obtaining all required **site/gate passes, permits, safety cards, and inductions** prior to the mobilization of INTEGRITY’s team.",
                "The safety of equipment and the inspection personnel during the inspection will be the Client’s responsibility, including any associated costs and arrangements.",
                "The Client shall provide full support to INTEGRITY’s team for proper execution of the job. This includes the provision of related instruments, safety equipment, and emergency medical facilities.",
                "The Client’s authorized representative or contractor at the site shall coordinate with INTEGRITY team to ensure all necessary arrangements are in place to facilitate the work.",
                "INTEGRITY shall not be held liable for delays caused by the Client, its subcontractors, or unforeseen circumstances beyond INTEGRITY control."
            ])
        ],
        BankAccounts:
        [
            new QuotationBankAccount(
                "Integrity International for Inspection", "Commercial International Bank (CIB)", "EGP",
                "100072853193", "EG640010008900000100072853193", "CIBEEGCX089"),
            new QuotationBankAccount(
                "Integrity International for Inspection", "Commercial International Bank (CIB)", "USD",
                "100072840806", "EG570010008900000100072840806", "CIBEEGCX089")
        ]);
}
