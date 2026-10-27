namespace GestionNegocios.Api.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Guarda un archivo subido en el filesystem bajo wwwroot/uploads/{subfolder}.
    /// Genera un nombre físico con GUID manteniendo la extensión sanitizada.
    /// Valida extensión, tipo MIME y tamaño máximo.
    /// </summary>
    /// <param name="file">Archivo recibido en la petición HTTP.</param>
    /// <param name="subfolder">Subcarpeta de destino (ej. "avatars", "ventas").</param>
    /// <param name="allowedExtensions">Extensiones permitidas en minúsculas (ej. [".jpg", ".png"]).</param>
    /// <param name="allowedMimeTypes">Tipos MIME válidos (ej. ["image/jpeg", "image/png"]).</param>
    /// <param name="maxSizeBytes">Tamaño máximo en bytes.</param>
    /// <returns>Tupla con la ruta relativa pública (ej. "/uploads/avatars/guid.jpg") o mensaje de error.</returns>
    Task<(string? relativePath, string? error)> SaveFileAsync(
        IFormFile file,
        string subfolder,
        string[] allowedExtensions,
        string[] allowedMimeTypes,
        long maxSizeBytes);

    /// <summary>
    /// Elimina un archivo físico del disco a partir de su ruta relativa almacenada en la base de datos.
    /// Evita path traversal y no genera excepciones si el archivo no existe.
    /// </summary>
    /// <param name="relativePath">Ruta relativa (ej. "/uploads/avatars/guid.jpg").</param>
    /// <returns>True si el archivo existía y se eliminó; False en caso contrario.</returns>
    bool DeleteFile(string? relativePath);

    /// <summary>
    /// Obtiene la información física de un archivo para su descarga o visualización segura por streaming.
    /// </summary>
    /// <param name="relativePath">Ruta relativa del archivo.</param>
    /// <returns>Tupla con existencia, ruta absoluta en disco, MIME type y nombre de descarga sugerido.</returns>
    (bool exists, string physicalPath, string contentType, string downloadFileName) GetFileInfo(string relativePath);
}
