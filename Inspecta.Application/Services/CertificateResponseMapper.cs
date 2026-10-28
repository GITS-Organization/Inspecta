using System.Text.Json;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

internal static class CertificateResponseMapper
{
    public static CertificateResponse Map(InspectionCertificate certificate) => new(
        certificate.Id,
        certificate.RequestId,
        certificate.RequestItemId,
        certificate.EquipmentId,
        certificate.InspectorId,
        certificate.TemplateId,
        JsonSerializer.Deserialize<Dictionary<string, string>>(certificate.TextsJson) ?? [],
        JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(certificate.DatesJson) ?? [],
        JsonSerializer.Deserialize<Dictionary<string, bool>>(certificate.AnswersJson) ?? [],
        certificate.FunctionCheck,
        certificate.FinalResult,
        certificate.SubmittedAt,
        certificate.ReviewNote,
        certificate.CertificateNumber,
        certificate.VerificationCode,
        certificate.ReviewedAt);
}
