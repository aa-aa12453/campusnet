using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CampusNet.Core.Configuration;
using CampusNet.Core.Models;

namespace CampusNet.Core.Credentials;

/// <summary>用 Windows DPAPI 加密保存拨号凭据，仅当前用户、本机可解密。</summary>
public static class CredentialStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static bool Exists() => File.Exists(AppPaths.CredentialFile);

    public static void Save(DialCredential cred)
    {
        AppPaths.EnsureDirs();
        string json = JsonSerializer.Serialize(cred, JsonOpts);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        try
        {
            byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(AppPaths.CredentialFile, cipher);
        }
        finally
        {
            Array.Clear(plain, 0, plain.Length);
        }
    }

    public static DialCredential? Load()
    {
        if (!Exists()) return null;
        try
        {
            byte[] cipher = File.ReadAllBytes(AppPaths.CredentialFile);
            byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            try
            {
                string json = Encoding.UTF8.GetString(plain);
                return JsonSerializer.Deserialize<DialCredential>(json, JsonOpts);
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        if (File.Exists(AppPaths.CredentialFile))
        {
            try { File.Delete(AppPaths.CredentialFile); } catch { }
        }
    }
}
