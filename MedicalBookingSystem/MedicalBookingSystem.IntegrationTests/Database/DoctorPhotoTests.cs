using System.Security.Claims;
using System.Text;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.BlazorServer.Service;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class DoctorPhotoTests
{
    [Theory]
    [InlineData(".png", SKEncodedImageFormat.Png)]
    [InlineData(".jpg", SKEncodedImageFormat.Jpeg)]
    public async Task ValidImage_IsDecodedResizedAndReencoded_AndReplacementDeletesPreviousFile(string extension, SKEncodedImageFormat format)
    {
        using var fixture = await Fixture.CreateAsync();
        using var stream = new MemoryStream(Picture(1200, 800, format).Concat(Encoding.UTF8.GetBytes("untrusted-appended-content")).ToArray());
        var result = await fixture.Uploader.ReplaceAsync(fixture.Scenario.DoctorId, stream, "../../client-name" + extension);
        Assert.True(result.IsSuccess, result.Error);
        var first = Assert.Single(Directory.GetFiles(fixture.Files.Directory));
        using (var decoded = SKBitmap.Decode(first)) { Assert.Equal(1024, decoded.Width); Assert.Equal(682, decoded.Height); }
        Assert.DoesNotContain("untrusted-appended-content", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(first)));
        using var secondStream = new MemoryStream(Picture(3, 2));
        Assert.True((await fixture.Uploader.ReplaceAsync(fixture.Scenario.DoctorId, secondStream, "second.png")).IsSuccess);
        Assert.False(File.Exists(first));
        var second = Assert.Single(Directory.GetFiles(fixture.Files.Directory));
        using var unit = fixture.Units.Create();
        Assert.Equal("https://media.example.test/media/doctors/" + Path.GetFileName(second), (await unit.Doctors.GetByIdAsync(fixture.Scenario.DoctorId))!.ImageUrl);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("mismatched")]
    [InlineData("oversized")]
    [InlineData("dimensions")]
    [InlineData("truncated")]
    [InlineData("extension")]
    public async Task InvalidInput_LeavesDatabaseAndFilesystemUnchanged(string kind)
    {
        using var fixture = await Fixture.CreateAsync();
        var bytes = kind switch
        {
            "text" => Encoding.UTF8.GetBytes("this is not an image"),
            "oversized" => new byte[DoctorImageUploader.MaximumBytes + 1],
            "dimensions" => Picture(4097, 1),
            "truncated" => Picture(3, 2)[..20],
            _ => Picture(3, 2)
        };
        using var stream = new MemoryStream(bytes);
        var result = await fixture.Uploader.ReplaceAsync(fixture.Scenario.DoctorId, stream,
            kind == "mismatched" ? "fake.jpg" : kind == "extension" ? "fake.svg" : "input.png");
        Assert.True(result.IsFailure);
        Assert.Empty(Directory.GetFiles(fixture.Files.Directory));
        using var unit = fixture.Units.Create();
        Assert.Equal("/images/no-image-icon.svg", (await unit.Doctors.GetByIdAsync(fixture.Scenario.DoctorId))!.ImageUrl);
    }

    [Fact]
    public async Task RejectedDatabaseSave_RemovesNewFile_AndPreservesOldPhoto()
    {
        var rejection = new RejectPhotoSave();
        using var fixture = await Fixture.CreateAsync(rejection);
        using var initial = new MemoryStream(Picture(3, 2));
        Assert.True((await fixture.Uploader.ReplaceAsync(fixture.Scenario.DoctorId, initial, "initial.png")).IsSuccess);
        var original = Assert.Single(Directory.GetFiles(fixture.Files.Directory));
        rejection.Active = true;
        using var replacement = new MemoryStream(Picture(4, 3));
        await Assert.ThrowsAsync<IOException>(() => fixture.Uploader.ReplaceAsync(fixture.Scenario.DoctorId, replacement, "replacement.png"));
        Assert.Equal(original, Assert.Single(Directory.GetFiles(fixture.Files.Directory)));
        using var unit = fixture.Units.Create();
        Assert.Equal(fixture.Files.Url(Path.GetFileName(original)), (await unit.Doctors.GetByIdAsync(fixture.Scenario.DoctorId))!.ImageUrl);
    }

    [Fact]
    public async Task OrphanSweep_DeletesOnlyOldOwnedUnreferencedFiles()
    {
        using var fixture = await Fixture.CreateAsync();
        using var stream = new MemoryStream(Picture(3, 2));
        Assert.True((await fixture.Uploader.ReplaceAsync(fixture.Scenario.DoctorId, stream, "current.png")).IsSuccess);
        var referenced = Assert.Single(Directory.GetFiles(fixture.Files.Directory));
        var orphan = Path.Combine(fixture.Files.Directory, Guid.NewGuid().ToString("N") + ".png");
        var pending = Path.Combine(fixture.Files.Directory, Guid.NewGuid().ToString("N") + ".png.pending");
        var young = Path.Combine(fixture.Files.Directory, Guid.NewGuid().ToString("N") + ".png");
        var unknown = Path.Combine(fixture.Files.Directory, "operator-managed.png");
        foreach (var path in new[] { orphan, pending, young, unknown }) await File.WriteAllTextAsync(path, "test");
        foreach (var path in new[] { orphan, pending, referenced, unknown }) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        await DoctorPhotoCleanup.SweepAsync(fixture.Units, fixture.Files, DateTime.UtcNow.AddDays(-1));
        Assert.False(File.Exists(orphan)); Assert.False(File.Exists(pending));
        Assert.True(File.Exists(young)); Assert.True(File.Exists(referenced)); Assert.True(File.Exists(unknown));
        fixture.Files.TryDeleteUrl("https://external.example.test/operator-managed.png");
        fixture.Files.TryDeleteUrl("https://media.example.test/media/doctors/../operator-managed.png");
        Assert.True(File.Exists(unknown));
    }

    [Fact]
    public async Task DoctorRole_CannotUploadEvenOwnPhoto()
    {
        using var fixture = await Fixture.CreateAsync();
        var principal = await ScenarioPrincipal.StaffAsync(fixture.Scope.ServiceProvider, "doctor@example.test");
        var access = ScenarioPrincipal.Access(fixture.Scope.ServiceProvider, principal);
        var uploader = new DoctorImageUploader(fixture.Units, access, fixture.Files);
        using var stream = new MemoryStream(Picture(3, 2));
        await Assert.ThrowsAsync<AccessDeniedException>(() => uploader.ReplaceAsync(fixture.Scenario.DoctorId, stream, "image.png"));
        Assert.Empty(Directory.GetFiles(fixture.Files.Directory));
    }

    private static byte[] Picture(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private sealed class RejectPhotoSave : SaveChangesInterceptor
    {
        public bool Active { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => Active ? throw new IOException("Simulated storage fault") : ValueTask.FromResult(result);
    }

    private sealed class CurrentUser(ClaimsPrincipal principal) : ICurrentUser
    { public Task<ClaimsPrincipal> GetPrincipalAsync() => Task.FromResult(principal); }

    private sealed class Fixture(SecurityScenario scenario) : IDisposable
    {
        public SecurityScenario Scenario { get; } = scenario;
        public IServiceScope Scope { get; } = scenario.Factory.Services.CreateScope();
        public IUnitOfWorkFactory Units => Scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        public DoctorPhotoFiles Files { get; private set; } = null!;
        public DoctorImageUploader Uploader { get; private set; } = null!;
        public static async Task<Fixture> CreateAsync(IInterceptor? interceptor = null)
        {
            var fixture = new Fixture(await SecurityScenario.CreateAsync(new MedicalApiFactory(interceptor: interceptor)));
            var path = Path.Combine(Path.GetTempPath(), "medical-photo-tests", Guid.NewGuid().ToString("N"));
            fixture.Files = new DoctorPhotoFiles(new MediaEnvironment { ContentRootPath = path }, Options.Create(new DoctorMediaOptions
                { PublicBaseUrl = "https://media.example.test", StoragePath = path }), NullLogger<DoctorPhotoFiles>.Instance);
            var principal = await ScenarioPrincipal.StaffAsync(fixture.Scope.ServiceProvider, "admin@example.test");
            var access = ScenarioPrincipal.Access(fixture.Scope.ServiceProvider, principal);
            fixture.Uploader = new DoctorImageUploader(fixture.Units, access, fixture.Files);
            return fixture;
        }
        public void Dispose() { Scope.Dispose(); Scenario.Dispose(); Directory.Delete(Files.Directory, true); }
    }

    private sealed class MediaEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
