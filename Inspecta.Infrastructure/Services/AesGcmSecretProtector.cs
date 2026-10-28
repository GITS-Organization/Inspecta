using System.Security.Cryptography;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Inspecta.Infrastructure.Services;

public sealed class AesGcmSecretProtector(IConfiguration configuration) : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(ciphertext)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        var parts = protectedValue.Split('.');
        if (parts.Length != 4 || parts[0] != "v1")
            throw new CryptographicException("Encrypted secret format is invalid or unsupported.");

        byte[] nonce;
        byte[] tag;
        byte[] ciphertext;
        try
        {
            nonce = Convert.FromBase64String(parts[1]);
            tag = Convert.FromBase64String(parts[2]);
            ciphertext = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Encrypted secret encoding is invalid.", exception);
        }

        if (nonce.Length != NonceSize || tag.Length != TagSize)
            throw new CryptographicException("Encrypted secret has invalid nonce or tag size.");

        var key = GetKey();
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return System.Text.Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private byte[] GetKey()
    {
        var configuredKey = configuration["IMAP_ENCRYPTION_KEY"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new EmailEncryptionConfigurationException(
                "Set IMAP_ENCRYPTION_KEY to a Base64-encoded 32-byte key before configuring coordinator email.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new EmailEncryptionConfigurationException("IMAP_ENCRYPTION_KEY must be valid Base64.", exception);
        }

        if (key.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new EmailEncryptionConfigurationException("IMAP_ENCRYPTION_KEY must decode to exactly 32 bytes.");
        }

        return key;
    }
}
