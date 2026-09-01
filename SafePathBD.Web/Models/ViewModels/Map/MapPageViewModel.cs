namespace SafePathBD.Web.Models.ViewModels.Map;

public class MapPageViewModel
{
    public double DefaultLatitude { get; init; }

    public double DefaultLongitude { get; init; }

    public int DefaultZoom { get; init; }

    public IReadOnlyList<string> ServiceTypes { get; init; } = Array.Empty<string>();
}
