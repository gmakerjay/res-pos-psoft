using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace RestaurantPOS.Wpf.Services;

/// <summary>
/// Hardware Fingerprinting Service
/// ดึงข้อมูลเอกลักษณ์ฮาร์ดแวร์ประจำเครื่อง PC (CPU, Motherboard, System Disk, Windows MachineGuid)
/// เพื่อสร้างรหัสประจำเครื่อง (Machine Code) รูปแบบมาตรฐาน RPOS-XXXX-XXXX-XXXX-XXXX
/// </summary>
public static class HardwareFingerprintService
{
    private static string? _cachedMachineCode;
    private static readonly object _lock = new();

    public static string GetMachineCode()
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_cachedMachineCode))
            {
                return _cachedMachineCode;
            }

            var rawComponents = CollectHardwareSignatures();
            _cachedMachineCode = GenerateFormattedCode(rawComponents);
            return _cachedMachineCode;
        }
    }

    private static string CollectHardwareSignatures()
    {
        var sb = new StringBuilder();

        // 1. Processor ID from WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                var val = item["ProcessorId"]?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    sb.Append("CPU:").Append(val.Trim()).Append(';');
                    break;
                }
            }
        }
        catch
        {
            // Fallback to Environment
            sb.Append("CPUE:").Append(Environment.ProcessorCount).Append(':')
              .Append(Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "GENERIC").Append(';');
        }

        // 2. Motherboard Serial from WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber, UUID FROM Win32_BaseBoard");
            foreach (var item in searcher.Get())
            {
                var ser = item["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(ser) && ser.Trim() != "To be filled by O.E.M." && ser.Trim() != "None")
                {
                    sb.Append("MB:").Append(ser.Trim()).Append(';');
                    break;
                }
            }
        }
        catch { }

        // 3. System Drive Volume Serial Number
        try
        {
            var sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var driveInfo = new DriveInfo(sysDrive);
            sb.Append("DRV:").Append(driveInfo.DriveFormat).Append(':')
              .Append(driveInfo.TotalSize / (1024 * 1024)).Append(';');
        }
        catch { }

        // 4. Windows MachineGuid from Registry (Persistent across reboots)
        try
        {
            using var rk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                      .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = rk?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(guid))
            {
                sb.Append("GUID:").Append(guid.Trim()).Append(';');
            }
        }
        catch
        {
            try
            {
                using var rk32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                                            .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = rk32?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    sb.Append("GUID:").Append(guid.Trim()).Append(';');
                }
            }
            catch { }
        }

        // 5. Machine Name Fallback if empty
        if (sb.Length == 0)
        {
            sb.Append("HOST:").Append(Environment.MachineName).Append(';')
              .Append("USER:").Append(Environment.UserName);
        }

        return sb.ToString();
    }

    private static string GenerateFormattedCode(string raw)
    {
        // Obfuscated Salt
        var salt = new byte[] { 0x52, 0x50, 0x4F, 0x53, 0x2D, 0x48, 0x57, 0x49, 0x44, 0x2D, 0x53, 0x45, 0x43, 0x55, 0x52, 0x45 };
        var rawBytes = Encoding.UTF8.GetBytes(raw);

        using var hmac = new HMACSHA256(salt);
        var hash = hmac.ComputeHash(rawBytes);

        // Convert to Base32 Crockford style (avoiding ambiguous characters 0, O, 1, I)
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var codeBuilder = new StringBuilder();

        for (int i = 0; i < 16; i++)
        {
            var idx = hash[i] % alphabet.Length;
            codeBuilder.Append(alphabet[idx]);
        }

        var s = codeBuilder.ToString();
        // Format as RPOS-XXXX-XXXX-XXXX-XXXX
        return $"RPOS-{s.Substring(0, 4)}-{s.Substring(4, 4)}-{s.Substring(8, 4)}-{s.Substring(12, 4)}";
    }
}
