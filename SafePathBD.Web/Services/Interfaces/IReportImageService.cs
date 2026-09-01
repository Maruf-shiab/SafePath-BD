namespace SafePathBD.Web.Services.Interfaces;

public sealed record StoredImage(string StorageKey, string PhysicalPath, string OriginalFileName);

public sealed record ImageValidationResult(bool IsValid, string? Error = null)
{
    public static readonly ImageValidationResult Valid = new(true);

    public static ImageValidationResult Invalid(string error) => new(false, error);
}

public interface IReportImageService
{
    int MaxImagesPerReport { get; }

    long MaxBytesPerImage { get; }

    IReadOnlyList<string> AllowedExtensions { get; }

    ImageValidationResult Validate(IFormFile file);

    ImageValidationResult ValidateSet(IReadOnlyCollection<IFormFile> files);

    /// <summary>Writes the files to disk and returns their metadata. Never trusts the client file name.</summary>
    Task<IReadOnlyList<StoredImage>> SaveAsync(IReadOnlyCollection<IFormFile> files, CancellationToken cancellationToken = default);

    /// <summary>Maps a stored key to a physical path, or null when the key escapes the storage root.</summary>
    string? ResolvePhysicalPath(string storageKey);

    string GetContentType(string storageKey);

    void DeleteQuietly(IEnumerable<StoredImage> images);
}
