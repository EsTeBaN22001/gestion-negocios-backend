using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace GestionNegocios.Api.Tests;

public class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "GestionNegocios.Api";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class FakeFileStorageService : IFileStorageService
{
    public List<string> DeletedFiles { get; } = new();
    public string? RelativePathToReturn { get; set; } = "/uploads/test/file.png";
    public string? ErrorToReturn { get; set; }
    public bool FileExistsToReturn { get; set; } = true;
    public string PhysicalPathToReturn { get; set; } = string.Empty;

    public Task<(string? relativePath, string? error)> SaveFileAsync(
        IFormFile file,
        string subfolder,
        string[] allowedExtensions,
        string[] allowedMimeTypes,
        long maxSizeBytes)
    {
        return Task.FromResult((RelativePathToReturn, ErrorToReturn));
    }

    public bool DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        DeletedFiles.Add(relativePath);
        return true;
    }

    public (bool exists, string physicalPath, string contentType, string downloadFileName) GetFileInfo(string relativePath)
    {
        return (FileExistsToReturn, PhysicalPathToReturn, "application/pdf", "test.pdf");
    }
}
