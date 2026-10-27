namespace GestionNegocios.Api.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;

    private static readonly Dictionary<string, string> MimeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".webp", "image/webp" },
        { ".pdf", "application/pdf" }
    };

    public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    private string GetUploadsBasePath()
    {
        var root = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }
        return Path.Combine(root, "uploads");
    }

    public async Task<(string? relativePath, string? error)> SaveFileAsync(
        IFormFile file,
        string subfolder,
        string[] allowedExtensions,
        string[] allowedMimeTypes,
        long maxSizeBytes)
    {
        if (file == null || file.Length == 0)
        {
            return (null, "No se ha proporcionado ningún archivo o el archivo está vacío.");
        }

        if (file.Length > maxSizeBytes)
        {
            var maxMb = Math.Round((double)maxSizeBytes / (1024 * 1024), 1);
            return (null, $"El archivo excede el tamaño máximo permitido de {maxMb} MB.");
        }

        var rawExtension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(rawExtension))
        {
            return (null, "El archivo debe tener una extensión válida.");
        }

        var extension = rawExtension.Trim().ToLowerInvariant();
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return (null, $"La extensión '{extension}' no está permitida. Extensiones válidas: {string.Join(", ", allowedExtensions)}.");
        }

        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (allowedMimeTypes.Length > 0 && !allowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            // Verificamos si al menos coincide con el MIME mapeado de la extensión
            if (!MimeTypeMappings.TryGetValue(extension, out var mappedMime) ||
                !allowedMimeTypes.Contains(mappedMime, StringComparer.OrdinalIgnoreCase))
            {
                return (null, $"El tipo de contenido '{contentType}' no es válido para este tipo de archivo.");
            }
        }

        // Sanitizar subfolder para evitar path traversal
        var sanitizedSubfolder = Path.GetFileName(subfolder.Trim());
        var uploadsBase = GetUploadsBasePath();
        var targetDir = Path.Combine(uploadsBase, sanitizedSubfolder);

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Generar nombre físico seguro mediante GUID
        var newFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(targetDir, newFileName);

        // Doble verificación contra path traversal
        var fullTargetDir = Path.GetFullPath(targetDir);
        var fullPhysicalPath = Path.GetFullPath(physicalPath);
        if (!fullPhysicalPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Posible intento de Path Traversal detectado: {FileName}", file.FileName);
            return (null, "Ruta de archivo no válida.");
        }

        try
        {
            await using var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al escribir el archivo en disco: {PhysicalPath}", physicalPath);
            return (null, "Ocurrió un error al guardar el archivo en el servidor.");
        }

        var relativePath = $"/uploads/{sanitizedSubfolder}/{newFileName}";
        _logger.LogInformation("Archivo guardado exitosamente: {RelativePath}", relativePath);
        return (relativePath, null);
    }

    public bool DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            var (exists, physicalPath, _, _) = GetFileInfo(relativePath);
            if (exists && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                _logger.LogInformation("Archivo físico eliminado: {PhysicalPath}", physicalPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo físico: {RelativePath}", relativePath);
        }

        return false;
    }

    public (bool exists, string physicalPath, string contentType, string downloadFileName) GetFileInfo(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return (false, string.Empty, "application/octet-stream", "archivo");
        }

        // Normalizar la ruta relativa removiendo prefijo '/' o '\'
        var cleanRelPath = relativePath.Trim().TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var uploadsBase = GetUploadsBasePath();
        var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        // Puede ser /uploads/... o relativo a wwwroot
        var physicalPath = Path.Combine(root, cleanRelPath);
        var fullRoot = Path.GetFullPath(uploadsBase);
        var fullPhysical = Path.GetFullPath(physicalPath);

        // Prevenir path traversal fuera de uploads
        if (!fullPhysical.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Acceso denegado fuera de uploads: {Path}", relativePath);
            return (false, string.Empty, "application/octet-stream", "archivo");
        }

        var exists = File.Exists(physicalPath);
        var ext = Path.GetExtension(physicalPath);
        var contentType = MimeTypeMappings.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
        var fileName = Path.GetFileName(physicalPath);

        return (exists, physicalPath, contentType, fileName);
    }
}
