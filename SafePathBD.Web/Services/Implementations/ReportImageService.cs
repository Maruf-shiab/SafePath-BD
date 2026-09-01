using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

public sealed class ReportImageService : IReportImageService
{
    // Deliberately outside wwwroot: pending and non-public report images must never be
    // reachable through the static-file middleware.
    private const string StorageFolder = "storage/report-images";

    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly HashSet<string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ReportImageService> _logger;

    public ReportImageService(IWebHostEnvironment environment, ILogger<ReportImageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public int MaxImagesPerReport => 4;

    public long MaxBytesPerImage => 5 * 1024 * 1024;

    public IReadOnlyList<string> AllowedExtensions => Extensions;

    public ImageValidationResult Validate(IFormFile file)
    {
        if (file.Length <= 0)
        {
            return ImageValidationResult.Invalid($"\"{SafeDisplayName(file)}\" is empty.");
        }

        if (file.Length > MaxBytesPerImage)
        {
            return ImageValidationResult.Invalid($"\"{SafeDisplayName(file)}\" is larger than {MaxBytesPerImage / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !Extensions.Contains(extension))
        {
            return ImageValidationResult.Invalid($"\"{SafeDisplayName(file)}\" is not a JPG, PNG or WebP image.");
        }

        if (!ContentTypes.Contains(file.ContentType))
        {
            return ImageValidationResult.Invalid($"\"{SafeDisplayName(file)}\" has an unsupported content type.");
        }

        // A declared content type can be forged, so the file signature is checked too.
        if (!HasImageSignature(file))
        {
            return ImageValidationResult.Invalid($"\"{SafeDisplayName(file)}\" does not appear to be a real image.");
        }

        return ImageValidationResult.Valid;
    }

    public ImageValidationResult ValidateSet(IReadOnlyCollection<IFormFile> files)
    {
        if (files.Count > MaxImagesPerReport)
        {
            return ImageValidationResult.Invalid($"You can attach at most {MaxImagesPerReport} images.");
        }

        foreach (var file in files)
        {
            var result = Validate(file);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ImageValidationResult.Valid;
    }

    public async Task<IReadOnlyList<StoredImage>> SaveAsync(
        IReadOnlyCollection<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return Array.Empty<StoredImage>();
        }

        var relativeFolder = $"{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}";
        var physicalFolder = Path.Combine(StorageRoot, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        Directory.CreateDirectory(physicalFolder);

        var stored = new List<StoredImage>(files.Count);

        foreach (var file in files)
        {
            // The stored name is generated entirely by the server; the client name is never used in a path.
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"report-{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(physicalFolder, fileName);

            await using (var target = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(target, cancellationToken);
            }

            stored.Add(new StoredImage($"{relativeFolder}/{fileName}", physicalPath, SafeDisplayName(file)));
        }

        return stored;
    }

    public string? ResolvePhysicalPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        var key = storageKey.Replace('\\', '/').Trim('/');
        if (key.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(key))
        {
            return null;
        }

        var root = Path.GetFullPath(StorageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));

        // The resolved path must still sit inside the storage root.
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    public string GetContentType(string storageKey) => Path.GetExtension(storageKey).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    public void DeleteQuietly(IEnumerable<StoredImage> images)
    {
        foreach (var image in images)
        {
            try
            {
                if (File.Exists(image.PhysicalPath))
                {
                    File.Delete(image.PhysicalPath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not remove an orphaned report image.");
            }
        }
    }

    private string StorageRoot =>
        Path.Combine(_environment.ContentRootPath, StorageFolder.Replace('/', Path.DirectorySeparatorChar));

    private static bool HasImageSignature(IFormFile file)
    {
        Span<byte> header = stackalloc byte[12];

        using var stream = file.OpenReadStream();
        var read = stream.Read(header);
        if (read < 12)
        {
            return false;
        }

        // JPEG
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return true;
        }

        // PNG
        if (header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return true;
        }

        // WebP: "RIFF" .... "WEBP"
        return header[..4].SequenceEqual("RIFF"u8.ToArray())
               && header[8..12].SequenceEqual("WEBP"u8.ToArray());
    }

    private static string SafeDisplayName(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        return string.IsNullOrWhiteSpace(name) ? "image" : name;
    }
}
