using Microsoft.EntityFrameworkCore;
using SafePathBD.Web.Common;
using SafePathBD.Web.Data;
using SafePathBD.Web.Models.Entities;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>Shared pieces of the report-creation flow used by both subtype services.</summary>
internal static class ReportWorkflow
{
    /// <summary>Resolved by stable status code, never by a hardcoded id.</summary>
    public static async Task<ushort?> GetPendingStatusIdAsync(SafePathDbContext db, CancellationToken cancellationToken)
    {
        var status = await db.ReportStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StatusCode == ReportStatusCodes.Pending, cancellationToken);

        return status?.StatusId;
    }

    public static void AddImages(SafePathDbContext db, ulong reportId, IReadOnlyList<StoredImage> images)
    {
        foreach (var image in images)
        {
            db.ReportImages.Add(new ReportImages
            {
                ReportId = reportId,
                // Stores the storage key, not a public URL: images are served by an authorized action.
                ImageUrl = image.StorageKey,
                Caption = null
            });
        }
    }
}
