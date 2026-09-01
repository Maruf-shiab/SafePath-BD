namespace SafePathBD.Web.Models.ViewModels.Shared;

public sealed record PageHeaderChip(string Label, string CssClass = "chip--muted");

public class PageHeaderViewModel
{
    public string? Eyebrow { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IReadOnlyList<PageHeaderChip> Chips { get; init; } = Array.Empty<PageHeaderChip>();
}
