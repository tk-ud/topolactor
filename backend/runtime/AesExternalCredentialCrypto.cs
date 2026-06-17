using System.Security.Cryptography;
using System.Text;

namespace Topolactor.Runtime;

/// <summary>
/// AES-256-GCM credential crypto for the external credential vault.
/// Key reference format: "env:ENV_VAR_NAME" — key is base64-encoded 32 bytes from the env var.
/// Encrypted payload layout: [12-byte IV][N-byte ciphertext][16-byte GCM tag].
/// </summary>
public sealed class AesExternalCredentialCrypto : IExternalCredentialCrypto
{
    public string DecryptForRuntimeUse(byte[] encryptedPayload, string encryptionKeyReference)
    {
        ArgumentNullException.ThrowIfNull(encryptedPayload);
        if (string.IsNullOrWhiteSpace(encryptionKeyReference))
            throw new ArgumentException("encryptionKeyReference is required.", nameof(encryptionKeyReference));
        if (encryptedPayload.Length < 28)
            throw new InvalidOperationException("EXTERNAL_CREDENTIAL_ENCRYPTED_PAYLOAD_TOO_SHORT");

        var key = ResolveKey(encryptionKeyReference);
        var iv = encryptedPayload[..12];
        var tag = encryptedPayload[^16..];
        var ciphertext = encryptedPayload[12..^16];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Decrypt(iv, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public byte[] EncryptForVaultStorage(string plaintextPayload, string encryptionKeyReference)
    {
        if (string.IsNullOrEmpty(plaintextPayload))
            throw new ArgumentException("plaintextPayload is required.", nameof(plaintextPayload));
        if (string.IsNullOrWhiteSpace(encryptionKeyReference))
            throw new ArgumentException("encryptionKeyReference is required.", nameof(encryptionKeyReference));

        var key = ResolveKey(encryptionKeyReference);
        var iv = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextPayload);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Encrypt(iv, plaintextBytes, ciphertext, tag);

        var result = new byte[iv.Length + ciphertext.Length + tag.Length];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, iv.Length);
        tag.CopyTo(result, iv.Length + ciphertext.Length);
        return result;
    }

    public string ComputeTokenHash(string plaintextPayload)
    {
        if (string.IsNullOrEmpty(plaintextPayload))
            throw new ArgumentException("plaintextPayload is required.", nameof(plaintextPayload));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextPayload));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static byte[] ResolveKey(string keyReference)
    {
        if (!keyReference.StartsWith("env:", StringComparison.Ordinal))
            throw new InvalidOperationException("EXTERNAL_CREDENTIAL_ENCRYPTION_KEY_REFERENCE_UNSUPPORTED");

        var envVar = keyReference["env:".Length..];
        var value = Environment.GetEnvironmentVariable(envVar)
            ?? throw new InvalidOperationException("EXTERNAL_CREDENTIAL_ENCRYPTION_KEY_MISSING");

        var key = Convert.FromBase64String(value);
        if (key.Length != 32)
            throw new InvalidOperationException("EXTERNAL_CREDENTIAL_ENCRYPTION_KEY_INVALID_LENGTH");
        return key;
    }
}
