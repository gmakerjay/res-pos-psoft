using System;
using System.Text.RegularExpressions;
using RestaurantPOS.KeyGen.Services;
using RestaurantPOS.Wpf.Services;
using Xunit;

namespace RestaurantPOS.Tests;

public class ClientLicensingAndKeyGenTests
{
    [Fact]
    public void HardwareFingerprint_GeneratesConsistentDeterministicMachineCode()
    {
        // Act
        var code1 = HardwareFingerprintService.GetMachineCode();
        var code2 = HardwareFingerprintService.GetMachineCode();

        // Assert
        Assert.NotNull(code1);
        Assert.NotEmpty(code1);
        Assert.Equal(code1, code2);

        // Pattern: RPOS-XXXX-XXXX-XXXX-XXXX (24 characters)
        Assert.Matches(@"^RPOS-[2-9A-Z]{4}-[2-9A-Z]{4}-[2-9A-Z]{4}-[2-9A-Z]{4}$", code1);
    }

    [Fact]
    public void TamperProofTrialTracker_Calculates14CalendarDaysGracePeriod()
    {
        // Act
        var status = TamperProofTrialTracker.CheckTrial();

        // Assert
        Assert.NotNull(status);
        Assert.NotEmpty(status.MachineCode);
        Assert.True(status.DaysRemaining >= 0 && status.DaysRemaining <= 14);
        Assert.True(status.FirstRunDateUtc <= DateTime.UtcNow);
        Assert.True(status.ExpirationDateUtc > status.FirstRunDateUtc);

        var expectedExpiry = status.FirstRunDateUtc.AddDays(14);
        Assert.Equal(expectedExpiry.Date, status.ExpirationDateUtc.Date);
    }

    [Fact]
    public void KeyGenEngine_GeneratesSignedLifetimeKey_AndClientLicenseService_ValidatesSuccessfully()
    {
        // Arrange
        var machineCode = HardwareFingerprintService.GetMachineCode();
        var storeCode = "DEFAULT";

        // Act: Dev generates Lifetime Key for this customer machine
        var key = KeyGenEngine.GenerateKey(machineCode, storeCode, "FullLifetime");

        // Assert Key Format
        Assert.StartsWith("ACT-", key);
        Assert.Contains(".", key);

        // Act: Client POS verifies the key
        var result = ClientLicenseService.Instance.VerifyLicenseKey(key, machineCode);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsPermanentLifetime);
        Assert.False(result.IsTrial);
        Assert.False(result.IsExpired);
        Assert.Equal(machineCode, result.MachineCode);
        Assert.Equal(storeCode, result.StoreCode);
    }

    [Fact]
    public void ClientLicenseService_RejectsKey_WhenMachineCodeMismatch()
    {
        // Arrange: Generate key for machine A
        var targetMachine = "RPOS-9999-8888-7777-6666";
        var currentMachine = HardwareFingerprintService.GetMachineCode();
        var key = KeyGenEngine.GenerateKey(targetMachine, "DEFAULT", "FullLifetime");

        // Act: Attempt to activate on machine B
        var result = ClientLicenseService.Instance.VerifyLicenseKey(key, currentMachine);

        // Assert: MUST BE REJECTED
        Assert.False(result.IsValid);
        Assert.Contains("ไม่ตรงกับรหัสเครื่องนี้", result.StatusDescription);
    }

    [Fact]
    public void ClientLicenseService_RejectsTamperedSignature()
    {
        // Arrange
        var machineCode = HardwareFingerprintService.GetMachineCode();
        var key = KeyGenEngine.GenerateKey(machineCode, "DEFAULT", "FullLifetime");

        // Tamper with the signature portion (flip characters at the end)
        var tamperedKey = key.Substring(0, key.Length - 4) + "ZZZZ";

        // Act
        var result = ClientLicenseService.Instance.VerifyLicenseKey(tamperedKey, machineCode);

        // Assert: Cryptographic verification must fail
        Assert.False(result.IsValid);
        Assert.Contains("ไม่ถูกต้อง", result.StatusDescription);
    }

    [Fact]
    public void KeyGenEngine_GeneratesYearlyKey_Computes365Days()
    {
        // Arrange
        var machineCode = HardwareFingerprintService.GetMachineCode();
        var key = KeyGenEngine.GenerateKey(machineCode, "STORE-TEST", "FullYearly");

        // Act
        var result = ClientLicenseService.Instance.VerifyLicenseKey(key, machineCode);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.IsPermanentLifetime);
        Assert.False(result.IsTrial);
        Assert.True(result.DaysRemaining >= 364 && result.DaysRemaining <= 366);
    }
}
