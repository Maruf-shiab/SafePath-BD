using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Interfaces;

public sealed record MyReportsQuery(
    ulong UserId,
    string? ReportType = null,
    string? StatusCode = null,
    int Page = 1,
    int PageSize = 20);

public sealed record MapBounds(double MinLat, double MinLng, double MaxLat, double MaxLng)
{
    public bool IsValid =>
        GeoMath.IsValidCoordinate(MinLat, MinLng)
        && GeoMath.IsValidCoordinate(MaxLat, MaxLng)
        && MinLat <= MaxLat
        && MinLng <= MaxLng;
}

public interface IReportService
{
    Task<ReportLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<ReportSummaryDto>> GetMyReportsAsync(MyReportsQuery query, CancellationToken cancellationToken = default);

    Task<MyReportStatsDto> GetMyReportStatsAsync(ulong userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportSummaryDto>> GetRecentReportsForUserAsync(ulong userId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the report only when the viewer is allowed to see it: the owner, a
    /// moderator/administrator, or anyone when the report is public and verified.
    /// </summary>
    Task<ReportDetailsDto?> GetDetailsAsync(ulong reportId, ulong? viewerUserId, bool viewerIsStaff, CancellationToken cancellationToken = default);

    /// <summary>Public map markers: verified and public reports inside the given bounds only.</summary>
    Task<IReadOnlyList<MapReportDto>> GetPublicMapReportsAsync(MapBounds bounds, string? reportType, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the stored key for an image only when the viewer is allowed to see its report,
    /// using exactly the same rule as <see cref="GetDetailsAsync"/>.
    /// </summary>
    Task<ReportImageFileDto?> GetImageForViewerAsync(ulong imageId, ulong? viewerUserId, bool viewerIsStaff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Official moderation history for a report. Reviewer notes are internal context, so they
    /// are only included when <paramref name="includeNotes"/> is true (owner or staff).
    /// </summary>
    Task<IReadOnlyList<ReportVerificationEntryDto>> GetVerificationHistoryAsync(ulong reportId, bool includeNotes, CancellationToken cancellationToken = default);
}
