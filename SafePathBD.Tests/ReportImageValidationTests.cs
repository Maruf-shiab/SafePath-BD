using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SafePathBD.Web.Services.Implementations;

namespace SafePathBD.Tests;

public class ReportImageValidationTests
{
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] TextBytes = "this is definitely not an image at all"u8.ToArray();

    private static ReportImageService CreateService() =>
        new(new StubEnvironment(), NullLogger<ReportImageService>.Instance);

    private static IFormFile File(string name, string contentType, byte[] content, long? overrideLength = null)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, overrideLength ?? content.Length, "Images", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public void Validate_AcceptsAGenuineJpeg()
    {
        Assert.True(CreateService().Validate(File("photo.jpg", "image/jpeg", JpegHeader)).IsValid);
    }

    [Fact]
    public void Validate_AcceptsAGenuinePng()
    {
        Assert.True(CreateService().Validate(File("photo.png", "image/png", PngHeader)).IsValid);
    }

    [Theory]
    [InlineData("payload.exe", "application/octet-stream")]
    [InlineData("script.js", "text/javascript")]
    [InlineData("page.html", "text/html")]
    [InlineData("vector.svg", "image/svg+xml")]
    public void Validate_RejectsDisallowedExtensions(string fileName, string contentType)
    {
        Assert.False(CreateService().Validate(File(fileName, contentType, JpegHeader)).IsValid);
    }

    [Fact]
    public void Validate_RejectsADisallowedContentTypeEvenWithAnImageExtension()
    {
        Assert.False(CreateService().Validate(File("photo.jpg", "text/html", JpegHeader)).IsValid);
    }

    [Fact]
    public void Validate_RejectsAFileWhoseSignatureIsNotAnImage()
    {
        // A forged content type must not be enough to get a file accepted.
        Assert.False(CreateService().Validate(File("photo.jpg", "image/jpeg", TextBytes)).IsValid);
    }

    [Fact]
    public void Validate_RejectsAFileOverTheSizeLimit()
    {
        var service = CreateService();
        var oversized = File("big.jpg", "image/jpeg", JpegHeader, service.MaxBytesPerImage + 1);

        Assert.False(service.Validate(oversized).IsValid);
    }

    [Fact]
    public void Validate_RejectsAnEmptyFile()
    {
        Assert.False(CreateService().Validate(File("empty.jpg", "image/jpeg", Array.Empty<byte>())).IsValid);
    }

    [Fact]
    public void ValidateSet_RejectsMoreFilesThanAllowed()
    {
        var service = CreateService();
        var files = Enumerable.Range(0, service.MaxImagesPerReport + 1)
            .Select(i => File($"photo{i}.jpg", "image/jpeg", JpegHeader))
            .ToList();

        Assert.False(service.ValidateSet(files).IsValid);
    }

    [Fact]
    public void ValidateSet_AcceptsTheMaximumAllowedCount()
    {
        var service = CreateService();
        var files = Enumerable.Range(0, service.MaxImagesPerReport)
            .Select(i => File($"photo{i}.jpg", "image/jpeg", JpegHeader))
            .ToList();

        Assert.True(service.ValidateSet(files).IsValid);
    }

    [Fact]
    public void ValidateSet_AcceptsAnEmptySelection()
    {
        Assert.True(CreateService().ValidateSet(Array.Empty<IFormFile>()).IsValid);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\appsettings.json")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("")]
    public void ResolvePhysicalPath_RejectsKeysThatEscapeTheStorageRoot(string key)
    {
        Assert.Null(CreateService().ResolvePhysicalPath(key));
    }

    [Fact]
    public void ResolvePhysicalPath_ReturnsNullForAKeyThatDoesNotExist()
    {
        Assert.Null(CreateService().ResolvePhysicalPath("2026/09/report-missing.png"));
    }

    [Theory]
    [InlineData("2026/09/photo.png", "image/png")]
    [InlineData("2026/09/photo.webp", "image/webp")]
    [InlineData("2026/09/photo.jpg", "image/jpeg")]
    [InlineData("2026/09/photo.jpeg", "image/jpeg")]
    public void GetContentType_MapsTheStoredExtension(string key, string expected)
    {
        Assert.Equal(expected, CreateService().GetContentType(key));
    }

    [Fact]
    public async Task SaveAsync_StoresOutsideWwwrootWithAGeneratedName()
    {
        var service = CreateService();
        var files = new[] { File("../../evil name.png", "image/png", PngHeader) };

        var stored = await service.SaveAsync(files);

        try
        {
            var image = Assert.Single(stored);
            Assert.StartsWith("report-", Path.GetFileName(image.StorageKey));
            Assert.DoesNotContain("..", image.StorageKey, StringComparison.Ordinal);
            Assert.DoesNotContain("evil", image.StorageKey, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}", image.PhysicalPath);
            Assert.NotNull(service.ResolvePhysicalPath(image.StorageKey));
        }
        finally
        {
            service.DeleteQuietly(stored);
        }
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "safepath-tests");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "SafePathBD.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
    }
}
