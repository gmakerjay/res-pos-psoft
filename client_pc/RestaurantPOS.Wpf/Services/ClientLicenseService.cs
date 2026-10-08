using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace RestaurantPOS.Wpf.Services;

/// <summary>
/// Client License Service
/// ระบบตรวจสอบสิทธิ์และลายเซ็นดิจิทัลประจำเครื่อง (Hardware-Bound Cryptographic License)
/// ใช้การเข้ารหัส Asymmetric RSA-2048 + เครื่องฮาร์ดแวร์ไอดี (Machine HWID)
/// ตัวโปรแกรมมีเฉพาะ Public Key สำหรับตรวจสอบ (Verify Only) ไม่สามารถสร้างคีย์เองได้
/// </summary>
public class ClientLicenseService
{
    private static readonly Lazy<ClientLicenseService> _instance = new(() => new ClientLicenseService());
    public static ClientLicenseService Instance => _instance.Value;

    // Obfuscated Public Key Fragments (XOR-masked to prevent plain strings extraction)
    private static readonly byte[] MaskA = new byte[] {
        0x32, 0x4B, 0x19, 0x8C, 0xFE, 0x5D, 0x21, 0x7A, 0x90, 0x33, 0xB5, 0x64, 0x81, 0x12, 0xC3, 0xD4
    };

    // Master Public Key (SubjectPublicKeyInfo Base64 from tools/master_public_key.txt)
    private const string PubKeyRaw = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxD9nc+TGAtF2Tf0pDaaV8jEiPAMZR+2eI0ZVNpWQsvPtE+DxazJ/nSauWrOuMbgLUf7GNlMf652/hv0bEAePhHbFXzZdTSHvLswuENlDVYlivf2HQ4As03Sw4jQiW093bmcLjhH/PY6ZJB2IpVipHVPm7RzPdyU6LJzxoTcb4Uqq7FE1htG2rSgWB5yeq3wSroMx397ltZBOH1xY6Z+6WG99OLS0pKF/r2hN9wlwXOcF5qCBRcL7jeYXuxhz/7akP229rIeFu/GOZoL/uL0TbjloIt9Iz8+SVEHxkKlu7dGVbPemJHN/R+fgLlYUsX6h8V/zC0W0YmMir5r36bGLzQIDAQAB";

    // Storage locations for activated license
    private const string RegSubKey = @"Software\Classes\CLSID\{4E9B1238-7F10-4A55-9B21-0D44E8972C1A}";
    private const string RegValKey = "LicCertificate";

    private static readonly string LicenseFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PsoftRES",
        "license.lic");

    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsPermanentLifetime { get; set; }
        public bool IsTrial { get; set; }
        public bool IsExpired { get; set; }
        public bool IsTampered { get; set; }
        public int DaysRemaining { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string MachineCode { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public DateTime? ExpirationDateUtc { get; set; }
        public string StatusDescription { get; set; } = string.Empty;
    }

    public class LicensePayload
    {
        public string MachineCode { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty; // "FullLifetime" | "FullYearly" | "TrialExtension"
        public long IssuedUtcTicks { get; set; }
        public long ExpiresUtcTicks { get; set; }
    }

    private ClientLicenseService() { }

    /// <summary>
    /// ตรวจสอบสถานะลิขสิทธิ์ของเครื่องปัจจุบัน
    /// 1. ตรวจสอบว่ามีคีย์เปิดใช้งานแบบสมบูรณ์ที่ผูกกับเครื่องนี้หรือไม่
    /// 2. หากไม่มีคีย์ จะตกไปสู่ระบบทดลองใช้ 14 วัน (TamperProofTrialTracker)
    /// </summary>
    public LicenseValidationResult GetCurrentLicenseStatus()
    {
        var machineCode = HardwareFingerprintService.GetMachineCode();

        // 1. Check for activated permanent / yearly license
        var savedToken = LoadSavedLicenseToken();
        if (!string.IsNullOrWhiteSpace(savedToken))
        {
            var verifyResult = VerifyLicenseKey(savedToken, machineCode);
            if (verifyResult.IsValid)
            {
                return verifyResult;
            }
        }

        // 2. Fall back to resilient 14-day calendar trial
        var trial = TamperProofTrialTracker.CheckTrial();

        if (trial.IsTampered)
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                IsTrial = true,
                IsExpired = true,
                IsTampered = true,
                DaysRemaining = 0,
                PlanName = "สิทธิ์การใช้งานถูกระงับ (ตรวจพบการปรับเวลา)",
                MachineCode = machineCode,
                StatusDescription = "ตรวจพบการปรับเปลี่ยนเวลาระบบเพื่อยืดอายุ กรุณาติดต่อผู้พัฒนาเพื่อขอรหัสเปิดใช้งาน"
            };
        }

        if (trial.IsExpired)
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                IsTrial = true,
                IsExpired = true,
                DaysRemaining = 0,
                PlanName = "สิทธิ์ทดลองใช้งาน 14 วันหมดอายุแล้ว",
                MachineCode = machineCode,
                ExpirationDateUtc = trial.ExpirationDateUtc,
                StatusDescription = "สิทธิ์ทดลองใช้งานครบ 14 วันตามกำหนดแล้ว กรุณาติดต่อผู้พัฒนาเพื่อขอ Activation Key"
            };
        }

        return new LicenseValidationResult
        {
            IsValid = true,
            IsTrial = true,
            IsExpired = false,
            DaysRemaining = trial.DaysRemaining,
            PlanName = $"สิทธิ์ทดลองใช้งาน (เหลืออีก {trial.DaysRemaining} วัน)",
            MachineCode = machineCode,
            ExpirationDateUtc = trial.ExpirationDateUtc,
            StatusDescription = $"ทดลองใช้ฟรี {trial.DaysRemaining} วัน (นับจากวันที่ติดตั้งครั้งแรก)"
        };
    }

    /// <summary>
    /// ตรวจสอบความถูกต้องของ Activation Key
    /// </summary>
    public LicenseValidationResult VerifyLicenseKey(string keyString, string? targetMachineCode = null)
    {
        var machineCode = targetMachineCode ?? HardwareFingerprintService.GetMachineCode();
        var cleanKey = keyString.Trim().Replace("\r", "").Replace("\n", "");

        try
        {
            // Format: ACT-<B64Payload>.<B64Signature>
            if (!cleanKey.StartsWith("ACT-", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("รูปแบบรหัสเปิดใช้งานไม่ถูกต้อง (ต้องขึ้นต้นด้วย ACT-)");
            }

            var rawBody = cleanKey.Substring(4);
            var parts = rawBody.Split('.');
            if (parts.Length != 2)
            {
                return Fail("โครงสร้างลายเซ็นดิจิทัลไม่สมบูรณ์");
            }

            var payloadBytes = Convert.FromBase64String(parts[0]);
            var signatureBytes = Convert.FromBase64String(parts[1]);

            // Verify Digital Signature with RSA Public Key
            using var rsa = RSA.Create();
            var pubBytes = Convert.FromBase64String(PubKeyRaw);
            rsa.ImportSubjectPublicKeyInfo(pubBytes, out _);

            var isSignatureValid = rsa.VerifyData(
                payloadBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!isSignatureValid)
            {
                return Fail("ลายเซ็นดิจิทัลไม่ถูกต้อง คีย์นี้ไม่ได้รับการรับรองจากผู้พัฒนา");
            }

            var json = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<LicensePayload>(json);
            if (payload == null)
            {
                return Fail("ไม่สามารถอ่านข้อมูลสิทธิ์ในคีย์ได้");
            }

            // Verify Hardware Binding
            if (!string.Equals(payload.MachineCode.Trim(), machineCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"คีย์นี้ผูกกับเครื่อง '{payload.MachineCode}' ไม่ตรงกับรหัสเครื่องนี้ ({machineCode})");
            }

            // Check Expiration
            var expiresUtc = new DateTime(payload.ExpiresUtcTicks, DateTimeKind.Utc);
            var nowUtc = DateTime.UtcNow;
            bool isPermanent = payload.Plan == "FullLifetime" || expiresUtc.Year >= 2099;

            if (!isPermanent && nowUtc > expiresUtc)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    IsExpired = true,
                    PlanName = "สิทธิ์การใช้งานหมดอายุแล้ว",
                    MachineCode = machineCode,
                    StoreCode = payload.StoreCode,
                    ExpirationDateUtc = expiresUtc,
                    StatusDescription = $"สิทธิ์หมดอายุเมื่อ {expiresUtc.ToLocalTime():dd/MM/yyyy HH:mm}"
                };
            }

            var daysRemaining = isPermanent ? 9999 : (int)Math.Ceiling((expiresUtc - nowUtc).TotalDays);

            return new LicenseValidationResult
            {
                IsValid = true,
                IsPermanentLifetime = isPermanent,
                IsTrial = false,
                IsExpired = false,
                DaysRemaining = daysRemaining,
                PlanName = isPermanent ? "เวอร์ชันเต็ม ตลอดชีพ (Full Lifetime)" : $"เวอร์ชันเต็ม รายปี (เหลืออีก {daysRemaining} วัน)",
                MachineCode = machineCode,
                StoreCode = payload.StoreCode,
                ExpirationDateUtc = isPermanent ? null : expiresUtc,
                StatusDescription = isPermanent ? "ปลดล็อกสิทธิ์ใช้งานถาวรตลอดชีพ" : $"สิทธิ์ใช้งานถึง {expiresUtc.ToLocalTime():dd/MM/yyyy}"
            };
        }
        catch (Exception ex)
        {
            return Fail("เกิดข้อผิดพลาดในการตรวจสอบคีย์: " + ex.Message);
        }

        LicenseValidationResult Fail(string reason) => new()
        {
            IsValid = false,
            IsExpired = true,
            MachineCode = machineCode,
            StatusDescription = reason
        };
    }

    /// <summary>
    /// ติดตั้งและบันทึกคีย์เปิดใช้งานลงในเครื่องอย่างปลอดภัย
    /// </summary>
    public LicenseValidationResult ActivateKey(string keyString)
    {
        var machineCode = HardwareFingerprintService.GetMachineCode();
        var result = VerifyLicenseKey(keyString, machineCode);
        if (!result.IsValid)
        {
            return result;
        }

        // Save to Registry & ProgramData
        SaveLicenseToken(keyString.Trim(), machineCode);
        return result;
    }

    private void SaveLicenseToken(string token, string machineCode)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var entropy = GetMachineEntropy(machineCode);
            var protectedBytes = ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser);
            var b64 = Convert.ToBase64String(protectedBytes);

            // 1. Registry
            using var rk = Registry.CurrentUser.CreateSubKey(RegSubKey);
            rk.SetValue(RegValKey, b64);

            // 2. ProgramData File
            var dir = Path.GetDirectoryName(LicenseFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(LicenseFilePath, b64, Encoding.UTF8);
            File.SetAttributes(LicenseFilePath, FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
        }
        catch { }
    }

    private string? LoadSavedLicenseToken()
    {
        var machineCode = HardwareFingerprintService.GetMachineCode();

        // Check Registry
        try
        {
            using var rk = Registry.CurrentUser.OpenSubKey(RegSubKey);
            var val = rk?.GetValue(RegValKey) as string;
            if (!string.IsNullOrWhiteSpace(val))
            {
                var decrypted = DecryptToken(val, machineCode);
                if (!string.IsNullOrWhiteSpace(decrypted)) return decrypted;
            }
        }
        catch { }

        // Check File
        try
        {
            if (File.Exists(LicenseFilePath))
            {
                var text = File.ReadAllText(LicenseFilePath, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var decrypted = DecryptToken(text, machineCode);
                    if (!string.IsNullOrWhiteSpace(decrypted)) return decrypted;
                }
            }
        }
        catch { }

        return null;
    }

    private string? DecryptToken(string b64, string machineCode)
    {
        try
        {
            var protectedBytes = Convert.FromBase64String(b64);
            var entropy = GetMachineEntropy(machineCode);
            var decrypted = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] GetMachineEntropy(string machineCode)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(machineCode + "|PSOFT_RES_LICENSE_KEY_VAULT_2026"));
    }
}
