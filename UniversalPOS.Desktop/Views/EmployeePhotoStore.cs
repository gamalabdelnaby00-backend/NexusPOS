using System;
using System.IO;

namespace UniversalPOS.Desktop.Views;

/// <summary>Local folder for employee photos. The database stores only the file name (Employee.PhotoKey).</summary>
public static class EmployeePhotoStore
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "employee-photos");

    public static string PathFor(string key) => Path.Combine(Folder, Path.GetFileName(key));

    /// <summary>Copies the picked image into the store and returns its key.</summary>
    public static string Save(string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidOperationException("Only PNG, JPG and WebP images are supported.");
        var info = new FileInfo(sourcePath);
        if (!info.Exists || info.Length == 0) throw new InvalidOperationException("The selected image is empty or missing.");
        if (info.Length > MaxBytes) throw new InvalidOperationException("The image exceeds the 5 MB limit.");
        Directory.CreateDirectory(Folder);
        var key = $"{Guid.NewGuid():N}{ext}";
        File.Copy(sourcePath, PathFor(key), overwrite: false);
        return key;
    }

    public static void Delete(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        try { var p = PathFor(key); if (File.Exists(p)) File.Delete(p); } catch (IOException) { }
    }
}
