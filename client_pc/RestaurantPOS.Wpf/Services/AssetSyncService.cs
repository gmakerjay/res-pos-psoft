using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using RestaurantPOS.Shared.DTOs;

namespace RestaurantPOS.Wpf.Services;

/// <summary>
/// Handles downloading and caching remote image assets locally (like an online game client patcher),
/// ensuring fast rendering and offline resilience for the POS desktop interface.
/// </summary>
public static class AssetSyncService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static string GetStoreCacheDir(string storeCode)
    {
        var safeStoreCode = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", safeStoreCode, "images");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static async Task SyncProductImagesAsync(string baseUrl, string storeCode, List<ProductDto> products)
    {
        if (products == null || products.Count == 0) return;

        var cleanBaseUrl = baseUrl.TrimEnd('/');
        var cacheDir = GetStoreCacheDir(storeCode);
        int syncedCount = 0;

        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.ImageUrl)) continue;

            string remoteUrl = product.ImageUrl.Trim();
            if (!remoteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                remoteUrl = $"{cleanBaseUrl}/" + remoteUrl.TrimStart('/');
            }

            try
            {
                // Create deterministic filename based on image URL
                var ext = Path.GetExtension(new Uri(remoteUrl).AbsolutePath);
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5) ext = ".jpg";
                
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(remoteUrl)))[..16];
                var localFilePath = Path.Combine(cacheDir, $"{product.Id}_{hash}{ext}");

                if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 0)
                {
                    product.ImageUrl = localFilePath;
                }
                else
                {
                    // Download like game client asset patcher
                    var imageBytes = await _httpClient.GetByteArrayAsync(remoteUrl);
                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(localFilePath, imageBytes);
                        product.ImageUrl = localFilePath;
                        syncedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to remote URL if download fails
                product.ImageUrl = remoteUrl;
                PosLogger.Warn($"[Asset Sync] Failed to cache image for product '{product.Name}' ({remoteUrl}): {ex.Message}");
            }
        }

        if (syncedCount > 0)
        {
            PosLogger.Info($"[Asset Sync] Downloaded and cached {syncedCount} new product images for store: {storeCode}");
        }
    }
}
