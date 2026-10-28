using Inspecta.Application.Contracts.Email;

namespace Inspecta.Application.Interfaces;

public interface IEmailParserService
{
    ParsedIncomingEmail Parse(string body);
}
