using SafePathBD.Web.Models.DTOs.Reports;
using SafePathBD.Web.Models.Entities;

namespace SafePathBD.Web.Services.Interfaces;

public interface ILocationService
{
    /// <summary>
    /// Returns an existing location row for the same physical point, or adds a new one to the
    /// change tracker. The caller owns the transaction and calls SaveChanges.
    /// </summary>
    Task<Locations> ResolveOrCreateAsync(ReportLocationInput input, CancellationToken cancellationToken = default);
}
