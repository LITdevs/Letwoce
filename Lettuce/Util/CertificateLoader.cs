namespace Lettuce.Util;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class CertificateLoader
{
    public static X509Certificate2 GetOrCreateCertificate()
    {
        var certPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "letwoce", "cert.pfx");
        if (File.Exists(certPath))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(certPath, "leafy green", X509KeyStorageFlags.Exportable);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(certPath)!);

        using RSA rsaKey = RSA.Create(4096);

        var req = new CertificateRequest(
            new X500DistinguishedName($"CN=lettuce"),
            rsaKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(100)); // If you're playing a game of lettuce for 100 years im very worried

        File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, "leafy green"));

        return GetOrCreateCertificate();
    }
}
