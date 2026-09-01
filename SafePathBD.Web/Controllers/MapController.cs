using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Models.ViewModels.Map;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers;

public class MapController : Controller
{
    // Dhaka, used only as the initial camera position before the user shares a location.
    private const double FallbackLatitude = 23.8103;
    private const double FallbackLongitude = 90.4125;
    private const int FallbackZoom = 12;

    private readonly IEmergencyService _emergencyService;

    public MapController(IEmergencyService emergencyService)
    {
        _emergencyService = emergencyService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var types = await _emergencyService.GetServiceTypesAsync(cancellationToken);

        return View(new MapPageViewModel
        {
            DefaultLatitude = FallbackLatitude,
            DefaultLongitude = FallbackLongitude,
            DefaultZoom = FallbackZoom,
            ServiceTypes = types.Select(t => t.Name).ToList()
        });
    }
}
