using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafePathBD.Web.Models.DTOs.Auth;
using SafePathBD.Web.Models.ViewModels.Auth;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService authService, ILogger<AccountController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.RegisterAsync(
            new RegisterRequest(model.FullName, model.Email, model.Phone, model.Password),
            cancellationToken);

        switch (result.Status)
        {
            case RegisterStatus.Success:
                TempData["StatusMessage"] = "Your account is ready. Sign in to continue.";
                return RedirectToAction(nameof(Login));

            case RegisterStatus.EmailAlreadyRegistered:
                ModelState.AddModelError(nameof(model.Email), "An account already uses this email address.");
                break;

            case RegisterStatus.PhoneAlreadyRegistered:
                ModelState.AddModelError(nameof(model.Phone), "An account already uses this phone number.");
                break;

            case RegisterStatus.DefaultRoleMissing:
                ModelState.AddModelError(string.Empty, "Registration is temporarily unavailable. Please try again later.");
                break;
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ValidateCredentialsAsync(model.Email, model.Password, cancellationToken);

        if (result.Status == LoginStatus.AccountDisabled)
        {
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Contact a SafePath BD administrator.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var principal = await _authService.BuildPrincipalAsync(result.User!, cancellationToken);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
            });

        await _authService.RecordSuccessfulLoginAsync(result.User!.UserId, cancellationToken);

        _logger.LogInformation("User {UserId} signed in.", result.User!.UserId);

        return RedirectToLocalOrDashboard(returnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocalOrDashboard(string? returnUrl)
    {
        return Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction("Index", "Dashboard");
    }
}
