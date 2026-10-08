using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace RestaurantPOS.Wpf.Services;

/// <summary>
/// Tamper-Proof Multi-Layer Trial Tracker
/// ติดตามอายุสิทธิ์ทดลองใช้งาน 14 วันจริง (Calendar Days)
/// ป้องกันการลบโฟลเดอร์โปรแกรมแล้วลงใหม่เพื่อโกงเวลา (Anti-Reinstall)
/// ป้องกันการปรับนาฬิกาถอยหลัง (Anti-Clock Rollback)
/// โดยเก็บข้อมูลแบบเข้ารหัสข้าม 3 ชั้น: Registry CLSID, ProgramData, LocalAppData
/// </summary>
public static class TamperProofTrialTracker
{
    private const int TrialDurationDays = 14;

    // Registry stealth location disguised as COM Class
    private const string RegSubKey = @"Software\Classes\CLSID\{9F7C2B41-0D8E-4E62-BA19-5C32E504A7B8}";
    private const string RegValueName = "ConfigSeed";

    // File paths
    private static readonly string ProgramDataFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PsoftRES",
        ".sys_token.dat");

    private static readonly string LocalAppDataFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PsoftRES",
        ".session_seed.dat");

    private static readonly object _syncLock = new();

    public class TrialStatus
    {
        public bool IsExpired { get; set; }
        public bool IsTampered { get; set; }
        public int DaysRemaining { get; set; }
        public DateTime FirstRunDateUtc { get; set; }
        public DateTime ExpirationDateUtc { get; set; }
        public string MachineCode { get; set; } = string.Empty;
    }

    private class TrialRecord
    {
        public long FirstRunTicks { get; set; }
        public long LastSeenTicks { get; set; }
        public string MachineHash { get; set; } = string.Empty;
    }

    public static TrialStatus CheckTrial()
    {
        lock (_syncLock)
        {
            var machineCode = HardwareFingerprintService.GetMachineCode();
            var nowUtc = DateTime.UtcNow;

            // Load records from all available layers
            var regRecord = ReadRegistryRecord(machineCode);
            var progDataRecord = ReadFileRecord(ProgramDataFile, machineCode);
            var localAppRecord = ReadFileRecord(LocalAppDataFile, machineCode);

            // Find earliest first run date among any valid existing layer
            long earliestFirstRunTicks = 0;
            long latestSeenTicks = 0;

            void Consider(TrialRecord? rec)
            {
                if (rec == null || rec.FirstRunTicks <= 0) return;
                if (earliestFirstRunTicks == 0 || rec.FirstRunTicks < earliestFirstRunTicks)
                {
                    earliestFirstRunTicks = rec.FirstRunTicks;
                }
                if (rec.LastSeenTicks > latestSeenTicks)
                {
                    latestSeenTicks = rec.LastSeenTicks;
                }
            }

            Consider(regRecord);
            Consider(progDataRecord);
            Consider(localAppRecord);

            bool isNewMachine = earliestFirstRunTicks == 0;
            if (isNewMachine)
            {
                // First ever run on this physical machine
                earliestFirstRunTicks = nowUtc.Ticks;
                latestSeenTicks = nowUtc.Ticks;
            }

            var firstRunUtc = new DateTime(earliestFirstRunTicks, DateTimeKind.Utc);
            var lastSeenUtc = new DateTime(latestSeenTicks, DateTimeKind.Utc);

            // Clock Rollback Detection (tolerance: 1 hour)
            bool isTampered = false;
            if (!isNewMachine && nowUtc < lastSeenUtc.AddHours(-1))
            {
                // Current system time is earlier than the last time the program was seen running!
                isTampered = true;
            }

            // Update last seen to now (if not tampered)
            var currentSeenTicks = isTampered ? latestSeenTicks : Math.Max(latestSeenTicks, nowUtc.Ticks);

            var activeRecord = new TrialRecord
            {
                FirstRunTicks = earliestFirstRunTicks,
                LastSeenTicks = currentSeenTicks,
                MachineHash = machineCode
            };

            // Self-healing: Ensure all layers are updated with the authentic record
            SaveRegistryRecord(activeRecord, machineCode);
            SaveFileRecord(ProgramDataFile, activeRecord, machineCode);
            SaveFileRecord(LocalAppDataFile, activeRecord, machineCode);

            // Calculate true calendar days passed
            var elapsedDays = (nowUtc - firstRunUtc).TotalDays;
            var daysRemaining = (int)Math.Ceiling(TrialDurationDays - elapsedDays);
            if (daysRemaining < 0) daysRemaining = 0;

            var expirationDateUtc = firstRunUtc.AddDays(TrialDurationDays);
            var isExpired = isTampered || (nowUtc >= expirationDateUtc) || (daysRemaining <= 0);

            return new TrialStatus
            {
                IsExpired = isExpired,
                IsTampered = isTampered,
                DaysRemaining = isExpired ? 0 : daysRemaining,
                FirstRunDateUtc = firstRunUtc,
                ExpirationDateUtc = expirationDateUtc,
                MachineCode = machineCode
            };
        }
    }

    private static byte[] GetMachineEntropy(string machineCode)
    {
        // Internal salt combined with machine code
        var secretSalt = new byte[] { 0x43, 0x41, 0x54, 0x5F, 0x54, 0x52, 0x49, 0x41, 0x4C, 0x5F, 0x32, 0x30, 0x32, 0x36 };
        using var sha = SHA256.Create();
        var combined = Encoding.UTF8.GetBytes(machineCode + "|PSOFT_RES_TRIAL_PROTECTION_V1");
        var hash = sha.ComputeHash(combined);
        for (int i = 0; i < secretSalt.Length; i++)
        {
            hash[i] ^= secretSalt[i];
        }
        return hash;
    }

    private static TrialRecord? ReadRegistryRecord(string machineCode)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegSubKey);
            var val = key?.GetValue(RegValueName) as string;
            if (string.IsNullOrWhiteSpace(val)) return null;

            return DecryptRecord(val, machineCode);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveRegistryRecord(TrialRecord record, string machineCode)
    {
        try
        {
            var encrypted = EncryptRecord(record, machineCode);
            using var key = Registry.CurrentUser.CreateSubKey(RegSubKey);
            key.SetValue(RegValueName, encrypted);
        }
        catch { }
    }

    private static TrialRecord? ReadFileRecord(string filePath, string machineCode)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text)) return null;

            return DecryptRecord(text, machineCode);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveFileRecord(string filePath, TrialRecord record, string machineCode)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var encrypted = EncryptRecord(record, machineCode);
            File.WriteAllText(filePath, encrypted, Encoding.UTF8);

            // Set Hidden + System attributes to prevent casual inspection
            File.SetAttributes(filePath, FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
        }
        catch { }
    }

    private static string EncryptRecord(TrialRecord record, string machineCode)
    {
        var json = JsonSerializer.Serialize(record);
        var bytes = Encoding.UTF8.GetBytes(json);
        var entropy = GetMachineEntropy(machineCode);

        var protectedBytes = ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static TrialRecord? DecryptRecord(string b64, string machineCode)
    {
        try
        {
            var protectedBytes = Convert.FromBase64String(b64);
            var entropy = GetMachineEntropy(machineCode);

            var decrypted = ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);

            var rec = JsonSerializer.Deserialize<TrialRecord>(json);
            if (rec != null && rec.MachineHash == machineCode)
            {
                return rec;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
