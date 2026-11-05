namespace Inspecta.Domain.Entities;

/// <summary>The bytes of an issued certificate's PDF, kept apart from the certificate's row so that listing certificates stays light.</summary>
public sealed class IssuedCertificateContent
{
    private IssuedCertificateContent() { }

    public IssuedCertificateContent(Guid issuedCertificateFileId, byte[] bytes)
    {
        IssuedCertificateFileId = issuedCertificateFileId;
        Bytes = bytes;
    }

    public Guid IssuedCertificateFileId { get; private set; }
    public byte[] Bytes { get; private set; } = [];

    public void Replace(byte[] bytes) => Bytes = bytes;
}
