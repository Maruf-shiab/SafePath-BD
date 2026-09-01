using System.ComponentModel.DataAnnotations;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.Moderation;
using SafePathBD.Web.Models.DTOs.Reports;

namespace SafePathBD.Web.Models.ViewModels.Moderation;

public sealed class ModerationQueueFilterViewModel
{
    public string? Status { get; set; } = ReportStatusCodes.Pending;

    public string? Type { get; set; }

    public string? Severity { get; set; }

    public string? Risk { get; set; }

    [StringLength(120)]
    public string? Search { get; set; }

    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(Type)
        || !string.IsNullOrWhiteSpace(Severity)
        || !string.IsNullOrWhiteSpace(Risk)
        || !string.IsNullOrWhiteSpace(Search)
        || From is not null
        || To is not null;
}

public sealed class ModerationQueueViewModel
{
    public ModerationQueueFilterViewModel Filter { get; set; } = new();

    public PagedResult<ModerationQueueItemDto> Results { get; set; } =
        new(Array.Empty<ModerationQueueItemDto>(), 1, 20, 0);

    public ModerationCountsDto Counts { get; set; } = new(0, 0, 0, 0, 0, 0, 0);
}

public sealed class ModerationReviewViewModel
{
    public ModerationReportDto Report { get; set; } = null!;

    public PagedResult<ReportCommentDto> Comments { get; set; } =
        new(Array.Empty<ReportCommentDto>(), 1, 20, 0);
}

public sealed class ModerationDecisionViewModel
{
    [Required]
    public string? TargetStatus { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    /// <summary>The status the reviewer saw when the page rendered, used to detect a stale decision.</summary>
    public string? ExpectedStatus { get; set; }
}
