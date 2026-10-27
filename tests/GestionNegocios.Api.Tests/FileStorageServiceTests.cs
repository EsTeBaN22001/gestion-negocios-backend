using System.Text;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class FileStorageServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FileStorageService _storageService;

    public FileStorageServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "gestion_negocios_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        var fakeEnv = new FakeWebHostEnvironment
        {
            WebRootPath = _tempDirectory,
            ContentRootPath = _tempDirectory
        };

        _storageService = new FileStorageService(fakeEnv, NullLogger<FileStorageService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static IFormFile CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task SaveFileAsync_ValidImage_SavesSuccessfullyAndReturnsRelativePath()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("fake-image-content");
        var formFile = CreateMockFormFile("avatar.png", "image/png", content);
        var allowedExts = new[] { ".png", ".jpg", ".jpeg" };
        var allowedMimes = new[] { "image/png", "image/jpeg" };

        // Act
        var (relativePath, error) = await _storageService.SaveFileAsync(
            formFile,
            "avatars",
            allowedExts,
            allowedMimes,
            2 * 1024 * 1024);

        // Assert
        Assert.Null(error);
        Assert.NotNull(relativePath);
        Assert.StartsWith("/uploads/avatars/", relativePath);
        Assert.EndsWith(".png", relativePath);

        var (exists, physicalPath, contentType, _) = _storageService.GetFileInfo(relativePath);
        Assert.True(exists);
        Assert.True(File.Exists(physicalPath));
        Assert.Equal("image/png", contentType);
    }

    [Fact]
    public async Task SaveFileAsync_DisallowedExtension_ReturnsError()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("malicious-script");
        var formFile = CreateMockFormFile("script.exe", "application/x-msdownload", content);

        // Act
        var (relativePath, error) = await _storageService.SaveFileAsync(
            formFile,
            "avatars",
            new[] { ".png", ".jpg" },
            new[] { "image/png", "image/jpeg" },
            1024 * 1024);

        // Assert
        Assert.NotNull(error);
        Assert.Null(relativePath);
        Assert.Contains("no está permitida", error);
    }

    [Fact]
    public async Task SaveFileAsync_ExceedsSizeLimit_ReturnsError()
    {
        // Arrange
        var content = new byte[100];
        var formFile = CreateMockFormFile("photo.jpg", "image/jpeg", content);

        // Act
        var (relativePath, error) = await _storageService.SaveFileAsync(
            formFile,
            "avatars",
            new[] { ".jpg" },
            new[] { "image/jpeg" },
            50); // limit 50 bytes

        // Assert
        Assert.NotNull(error);
        Assert.Null(relativePath);
        Assert.Contains("excede el tamaño máximo", error);
    }

    [Fact]
    public async Task DeleteFile_ExistingFile_DeletesPhysicalFileAndReturnsTrue()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("dummy");
        var formFile = CreateMockFormFile("doc.pdf", "application/pdf", content);
        var (relativePath, _) = await _storageService.SaveFileAsync(
            formFile,
            "ventas",
            new[] { ".pdf" },
            new[] { "application/pdf" },
            1024 * 1024);

        Assert.NotNull(relativePath);
        var (existsBefore, physicalPath, _, _) = _storageService.GetFileInfo(relativePath);
        Assert.True(existsBefore);

        // Act
        var deleted = _storageService.DeleteFile(relativePath);

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(physicalPath));
    }

    [Fact]
    public void GetFileInfo_PathTraversalAttempt_ReturnsNonExistent()
    {
        // Act
        var (exists, _, _, _) = _storageService.GetFileInfo("/../../Windows/System32/calc.exe");

        // Assert
        Assert.False(exists);
    }
}
