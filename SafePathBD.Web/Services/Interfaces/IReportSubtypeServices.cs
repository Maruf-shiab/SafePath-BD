using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Services.Interfaces;

public interface IAccidentReportService
{
    Task<CreateReportResult> CreateAsync(
        CreateAccidentReportRequest request,
        IReadOnlyList<StoredImage> images,
        CancellationToken cancellationToken = default);
}

public interface IHazardReportService
{
    Task<CreateReportResult> CreateAsync(
        CreateHazardReportRequest request,
        IReadOnlyList<StoredImage> images,
        CancellationToken cancellationToken = default);
}
