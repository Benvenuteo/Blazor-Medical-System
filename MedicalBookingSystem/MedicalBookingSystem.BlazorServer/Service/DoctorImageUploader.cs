using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.SharedKernel;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace MedicalBookingSystem.BlazorServer.Service;

public sealed class DoctorImageUploader(IUnitOfWorkFactory units, UserAccess access, DoctorPhotoFiles files)
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    public async Task<Result> ReplaceAsync(int doctorId, Stream source, string fileName)
    {
        await access.RequireAdminAsync();
        using var unit = units.Create();
        var doctor = await unit.Doctors.GetByIdAsync(doctorId);
        if (doctor is null) return Result.Failure("Lekarz nie istnieje.", ErrorType.NotFound);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png")) return InvalidImage();
        using var buffer = new MemoryStream();
        var block = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(block)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) return Result.Failure("Maksymalny rozmiar zdjęcia to 10 MB.");
            buffer.Write(block, 0, read);
        }
        buffer.Position = 0;
        using var data = SKData.CreateCopy(buffer.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 4096 || codec.Info.Height > 4096 ||
            codec.FrameCount > 1 || (extension == ".png" ? codec.EncodedFormat != SKEncodedImageFormat.Png : codec.EncodedFormat != SKEncodedImageFormat.Jpeg))
            return InvalidImage();
        using var pixels = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(pixels.Info, pixels.GetPixels()) != SKCodecResult.Success) return InvalidImage();
        var scale = Math.Min(1d, 1024d / Math.Max(pixels.Width, pixels.Height));
        using var resized = new SKBitmap(new SKImageInfo(Math.Max(1, (int)(pixels.Width * scale)), Math.Max(1, (int)(pixels.Height * scale))));
        if (!pixels.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear))) return InvalidImage();
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        {
            var name = Guid.NewGuid().ToString("N") + ".png";
            var path = Path.Combine(files.Directory, name);
            var pending = path + ".pending";
            var committed = false;
            try
            {
                // Re-encode pixels only. Client paths, metadata and appended content are never published.
                await File.WriteAllBytesAsync(pending, encoded.ToArray());
                File.Move(pending, path);
                var oldUrl = doctor.ImageUrl;
                doctor.ImageUrl = files.Url(name);
                await unit.SaveChangesAsync();
                committed = true;
                // A failed deletion is logged and retried by the orphan sweep, without undoing a successful DB save.
                await files.TryDeleteUnreferencedAsync(units, oldUrl);
                return Result.Success();
            }
            finally
            {
                files.TryDeletePath(pending);
                if (!committed) files.TryDeletePath(path);
            }
        }
    }

    private static Result InvalidImage() => Result.Failure("Wybierz poprawny, pojedynczy obraz JPG lub PNG o wymiarach do 4096 × 4096 pikseli.");
}

public sealed class DoctorMediaOptions
{
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string StoragePath { get; set; } = "data/media/doctors";
}

public sealed class DoctorPhotoFiles
{
    private readonly string _publicBase;
    private readonly ILogger<DoctorPhotoFiles> _logger;
    public string Directory { get; }

    public DoctorPhotoFiles(IWebHostEnvironment environment, IOptions<DoctorMediaOptions> options, ILogger<DoctorPhotoFiles> logger)
    {
        _logger = logger;
        _publicBase = options.Value.PublicBaseUrl.TrimEnd('/') + "/media/doctors/";
        Directory = Path.GetFullPath(options.Value.StoragePath, environment.ContentRootPath);
        System.IO.Directory.CreateDirectory(Directory);
    }

    public string Url(string name) => _publicBase + name;

    public async Task TryDeleteUnreferencedAsync(IUnitOfWorkFactory units, string url)
    {
        try
        {
            using var check = units.Create();
            if (!await check.Doctors.AnyAsync(d => d.ImageUrl == url)) TryDeleteUrl(url);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { _logger.LogWarning(exception, "Photo replacement succeeded, but reference cleanup must be retried by the orphan sweep."); }
    }

    public void TryDeleteUrl(string url)
    {
        if (!url.StartsWith(_publicBase, StringComparison.Ordinal)) return;
        var name = url[_publicBase.Length..];
        if (IsOwnedName(name)) TryDeletePath(Path.Combine(Directory, name));
    }

    internal static bool IsOwnedName(string name) => name.EndsWith(".png", StringComparison.Ordinal) &&
        name.Length == 36 && Guid.TryParseExact(name[..32], "N", out _);

    public void TryDeletePath(string path)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) != Directory) return;
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { _logger.LogWarning(exception, "Could not delete an obsolete doctor photo {FileName}", Path.GetFileName(path)); }
    }
}
