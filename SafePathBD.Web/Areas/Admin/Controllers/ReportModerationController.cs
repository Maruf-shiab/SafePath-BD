using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.ViewModels.Moderation;
using SafePathBD.Web.Security;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Areas.Admin.Controllers;

/// <summary>
/// The review workspace. Open to moderators and administrators; deliberately does not
/// grant any other administrative capability.
/// </summary>
[Area("Admin")]
[Authorize(Roles = RoleNames.AdminOrModerator)]
public class ReportModerationController : Controller
{
    private readonly IReportModerationService _moderation;
    private readonly IReportCommunityService _community;

    public ReportModerationController(IReportModerationService moderation, IReportCommunityService community)
    {
        _moderation = moderation;
        _community = community;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ModerationQueueFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter ??= new ModerationQueueFilterViewModel();

        var query = new ModerationQueueQuery(
            StatusCode: Normalize(filter.Status),
            ReportType: Normalize(filter.Type),
            Search: filter.Search,
            Severity: filter.Severity,
            RiskLevel: Normalize(filter.Risk),
            From: filter.From,
            To: filter.To,
            Page: filter.Page,
            PageSize: 20);

        var model = new ModerationQueueViewModel
        {
            Filter = filter,
            Results = await _moderation.GetQueueAsync(query, cancellationToken),
            Counts = await _moderation.GetCountsAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Review(long id, CancellationToken cancellationToken)
    {
        var report = await _moderation.GetForReviewAsync((ulong)id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var viewer = new CommunityViewer(User.GetUserId(), true);

        return View(new ModerationReviewViewModel
        {
            Report = report,
            Comments = await _community.GetCommentsAsync((ulong)id, viewer, 1, 20, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(long id, ModerationDecisionViewModel form, CancellationToken cancellationToken)
    {
        var decision = new ModerationDecision(
            (ulong)id,
            User.GetUserId(),
            form.TargetStatus ?? string.Empty,
            form.Note,
            form.ExpectedStatus);

        var result = await _moderation.ApplyDecisionAsync(decision, cancellationToken);

        if (result.Succeeded)
        {
            TempData["ModerationMessage"] = result.CreatedAccidentId is not null
                ? $"Report verified and added to the trusted accident dataset."
                : $"Report moved to {result.NewStatusName}.";
            TempData["ModerationVariant"] = "success";
        }
        else
        {
            TempData["ModerationMessage"] = result.Message;
            TempData["ModerationVariant"] = result.Status == ModerationStatus.StaleState ? "warning" : "error";
        }

        return RedirectToAction(nameof(Review), new { id });
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
