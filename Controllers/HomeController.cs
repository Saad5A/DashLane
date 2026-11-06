using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DashLane.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MembershipService _membership;
    private readonly GoogleAuthOptions _google;
    private readonly OtpSender _otp;

    public HomeController(
        ILogger<HomeController> logger,
        MembershipService membership,
        GoogleAuthOptions google,
        OtpSender otp)
    {
        _logger = logger;
        _membership = membership;
        _google = google;
        _otp = otp;
    }

    public async Task<IActionResult> Index()
    {
        return View(new HomeIndexViewModel
        {
            MemberCount = await _membership.CountAsync()
        });
    }

    [HttpGet]
    public IActionResult SignIn()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Community));
        }

        ViewData["GoogleEnabled"] = _google.IsConfigured;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("SignIn")]
    public async Task<IActionResult> SignInPost(string email, string password, bool remember)
    {
        var member = await _membership.AuthenticateAsync(email ?? "", password ?? "");
        if (member == null)
        {
            ViewData["AuthNotice"] = "The email or password is incorrect.";
            ViewData["Email"] = email;
            ViewData["GoogleEnabled"] = _google.IsConfigured;
            return View("SignIn");
        }

        await SignInMember(member, remember);
        return RedirectToAction(nameof(Community));
    }

    [HttpGet]
    public async Task<IActionResult> SignUp()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Community));
        }

        ViewData["MemberCount"] = await _membership.CountAsync();
        ViewData["GoogleEnabled"] = _google.IsConfigured;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("SignUp")]
    public async Task<IActionResult> SignUpPost(string fullName, string email, string password, bool terms)
    {
        ViewData["MemberCount"] = await _membership.CountAsync();
        if (!terms)
        {
            ViewData["AuthNotice"] = "Agree to the Terms of Service and Privacy Policy to continue.";
            ViewData["FullName"] = fullName;
            ViewData["Email"] = email;
            ViewData["GoogleEnabled"] = _google.IsConfigured;
            return View("SignUp");
        }

        var (member, error) = await _membership.RegisterAsync(fullName ?? "", email ?? "", password ?? "");
        if (member == null)
        {
            ViewData["AuthNotice"] = error;
            ViewData["FullName"] = fullName;
            ViewData["Email"] = email;
            ViewData["GoogleEnabled"] = _google.IsConfigured;
            return View("SignUp");
        }

        await SignInMember(member, remember: true);
        return RedirectToAction(nameof(Community));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoogleStart(string? source, bool terms = false)
    {
        if (!_google.IsConfigured)
        {
            return await GoogleUnavailable(source);
        }

        if (string.Equals(source, "signup", StringComparison.OrdinalIgnoreCase) && !terms)
        {
            ViewData["AuthNotice"] = "Agree to the Terms of Service and Privacy Policy to continue with Google.";
            ViewData["GoogleEnabled"] = true;
            ViewData["MemberCount"] = await _membership.CountAsync();
            return View("SignUp");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback), "Home")
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback()
    {
        if (!_google.IsConfigured)
        {
            return RedirectToAction(nameof(SignIn));
        }

        var external = await HttpContext.AuthenticateAsync("External");
        if (!external.Succeeded || external.Principal == null)
        {
            ViewData["AuthNotice"] = "Google sign-in was cancelled or failed. Try again.";
            ViewData["GoogleEnabled"] = true;
            return View("SignIn");
        }

        var subject = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? external.Principal.FindFirstValue("sub");
        var email = external.Principal.FindFirstValue(ClaimTypes.Email)
            ?? external.Principal.FindFirstValue("email");
        var name = external.Principal.FindFirstValue(ClaimTypes.Name)
            ?? external.Principal.FindFirstValue("name")
            ?? "";

        await HttpContext.SignOutAsync("External");

        var (member, error) = await _membership.FindOrCreateGoogleAsync(subject ?? "", email ?? "", name);
        if (member == null)
        {
            ViewData["AuthNotice"] = error ?? "Google sign-in could not create an account.";
            ViewData["GoogleEnabled"] = true;
            return View("SignIn");
        }

        await SignInMember(member, remember: true);
        return RedirectToAction(nameof(Community));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public new async Task<IActionResult> SignOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> GoogleUnavailable(string? source)
    {
        ViewData["AuthNotice"] = "Google sign-in is not configured.";
        ViewData["GoogleEnabled"] = false;
        if (string.Equals(source, "signup", StringComparison.OrdinalIgnoreCase))
        {
            ViewData["MemberCount"] = await _membership.CountAsync();
            return View("SignUp");
        }

        return View("SignIn");
    }

    [Authorize]
    public IActionResult Community() => View();

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AboutYou()
    {
        return View(await CurrentMember());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AboutYou(string legalName, int? dobDay, int? dobMonth, int? dobYear, string? gender, string? homeCity, string? preferredLanguage)
    {
        var member = await CurrentMember();
        legalName = (legalName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(legalName))
        {
            ViewData["Notice"] = "Enter your full legal name.";
            return View(member);
        }

        if (dobDay is null || dobMonth is null || dobYear is null ||
            !TryDate(dobYear.Value, dobMonth.Value, dobDay.Value, out var dob))
        {
            ViewData["Notice"] = "Enter a real date of birth.";
            return View(member);
        }

        var age = Age(dob);
        if (age < 18)
        {
            ViewData["Notice"] = "You must be at least 18 years old.";
            return View(member);
        }

        member.LegalName = legalName;
        member.DateOfBirth = dob;
        member.Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim();
        member.HomeCity = string.IsNullOrWhiteSpace(homeCity) ? null : homeCity.Trim();
        member.PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? null : preferredLanguage.Trim();
        await _membership.SaveAsync(member);
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile() => View(await CurrentMember());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string? bio, string? languages, string? preferences, IFormFile? photo, bool removePhoto = false)
    {
        var member = await CurrentMember();
        if (removePhoto)
        {
            _membership.ClearPhoto(member);
            await _membership.SaveAsync(member);
            ViewData["Notice"] = "Photo removed.";
            return View(member);
        }

        bio = (bio ?? "").Trim();
        if (string.IsNullOrWhiteSpace(bio))
        {
            ViewData["Notice"] = "Add a short bio before continuing.";
            return View(member);
        }

        if (bio.Length > 300)
        {
            ViewData["Notice"] = "Keep the bio to 300 characters.";
            return View(member);
        }

        if (photo != null && photo.Length > 0)
        {
            try
            {
                member.PhotoFileName = await _membership.SaveFileAsync(member, photo, "photo");
            }
            catch (InvalidOperationException exception)
            {
                ViewData["Notice"] = exception.Message;
                return View(member);
            }
        }

        member.Bio = bio;
        member.Languages = CleanList(languages);
        member.Preferences = CleanList(preferences);
        member.ProfileCompletedUtc = DateTime.UtcNow;
        await _membership.SaveAsync(member);
        return RedirectToAction(nameof(Contact));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Contact()
    {
        ViewData["OtpChannel"] = _otp.ChannelLabel;
        return View(await CurrentMember());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string? phoneNumber, string? code, string intent)
    {
        var member = await CurrentMember();
        ViewData["OtpChannel"] = _otp.ChannelLabel;
        var normalized = MembershipService.NormalizeCanadianPhone(phoneNumber);
        if (normalized == null)
        {
            ViewData["Notice"] = "Enter a valid 10-digit Canadian mobile number.";
            return View(member);
        }

        member.PhoneNumber = normalized;
        if (intent == "send")
        {
            var issued = _membership.CreatePhoneCode(member);
            await _membership.SaveAsync(member);
            try
            {
                var (_, notice) = await _otp.SendAsync(member, issued);
                ViewData["Notice"] = notice;
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(exception, "OTP delivery rejected for member {MemberId}", member.Id);
                ViewData["Notice"] = exception.Message;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "OTP delivery failed for member {MemberId}", member.Id);
                ViewData["Notice"] = "We could not send the SMS verification code. Try again in a moment.";
            }

            return View(member);
        }

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6 || !code.Trim().All(char.IsDigit))
        {
            ViewData["Notice"] = "Enter the 6-digit code we sent you.";
            return View(member);
        }

        if (!_membership.PhoneCodeMatches(member, code ?? ""))
        {
            await _membership.SaveAsync(member);
            ViewData["Notice"] = member.PhoneCodeExpiresUtc < DateTime.UtcNow
                ? "That code has expired. Send a new one."
                : "That code does not match. Send a new code or try again.";
            return View(member);
        }

        member.PhoneVerified = true;
        member.PhoneCodeHash = null;
        member.PhoneCodeExpiresUtc = null;
        await _membership.SaveAsync(member);
        return RedirectToAction(nameof(Identity));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Identity() => View(await CurrentMember());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Identity(string? documentType, IFormFile? document, bool removeDocument = false)
    {
        var member = await CurrentMember();
        var allowed = new[] { "Driver's Licence", "Passport", "Provincial Photo ID" };
        if (removeDocument)
        {
            _membership.ClearDocument(member);
            member.IssuingCountry = "Canada";
            await _membership.SaveAsync(member);
            ViewData["Notice"] = "Document removed.";
            return View(member);
        }

        if (string.IsNullOrWhiteSpace(documentType) || !allowed.Contains(documentType))
        {
            ViewData["Notice"] = "Choose the document you are uploading.";
            return View(member);
        }

        if ((document == null || document.Length == 0) && string.IsNullOrWhiteSpace(member.DocumentFileName))
        {
            ViewData["Notice"] = "Upload a photo or PDF of the document.";
            return View(member);
        }

        if (document != null && document.Length > 0)
        {
            try
            {
                member.DocumentFileName = await _membership.SaveFileAsync(member, document, "document");
            }
            catch (InvalidOperationException exception)
            {
                ViewData["Notice"] = exception.Message;
                return View(member);
            }
        }

        member.DocumentType = documentType;
        member.IssuingCountry = "Canada";
        member.DocumentSubmittedUtc = DateTime.UtcNow;
        await _membership.SaveAsync(member);
        return RedirectToAction(nameof(Review));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Review() => View(await CurrentMember());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewConfirm()
    {
        var member = await CurrentMember();
        if (member.CompletedSteps < 4)
        {
            ViewData["Notice"] = "Finish each onboarding step before completing setup.";
            return View("Review", member);
        }

        member.OnboardingCompletedUtc = DateTime.UtcNow;
        await _membership.SaveAsync(member);
        ViewData["Notice"] = "Your profile is saved. The ride feed is not available yet.";
        return View("Review", member);
    }

    [Authorize]
    public async Task<IActionResult> Photo()
    {
        var member = await CurrentMember();
        var path = _membership.MemberFilePath(member, member.PhotoFileName);
        return path == null ? NotFound() : PhysicalFile(path, ContentType(path));
    }

    public IActionResult Terms() => View();

    public IActionResult Privacy() => View();

    public IActionResult SecurityWhitepaper() => View();

    public IActionResult CommunityStandards() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string? returnUrl)
    {
        var supported = new[] { "en-CA", "fr-CA", "en-US" };
        if (!supported.Contains(culture))
        {
            culture = "en-CA";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CitySearch(string? q)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2)
        {
            return Json(Array.Empty<object>());
        }

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("nominatim");
        var url = "https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&limit=6&countrycodes=ca&q=" +
                  Uri.EscapeDataString(q);
        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return Json(Array.Empty<object>());
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var results = new List<object>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var display = item.GetProperty("display_name").GetString() ?? "";
            string? city = null;
            if (item.TryGetProperty("address", out var address))
            {
                city = FirstString(address, "city", "town", "village", "municipality", "hamlet");
                var province = FirstString(address, "state", "province");
                if (!string.IsNullOrWhiteSpace(city))
                {
                    display = string.IsNullOrWhiteSpace(province) ? city + ", Canada" : city + ", " + province;
                }
            }

            results.Add(new { label = display, value = display });
        }

        return Json(results);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<Member> CurrentMember()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (await _membership.FindAsync(id))!;
    }

    private async Task SignInMember(Member member, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, member.Id.ToString()),
            new(ClaimTypes.Email, member.Email),
            new(ClaimTypes.Name, member.FullName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = remember,
                ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(14) : null
            });
    }

    private static bool TryDate(int year, int month, int day, out DateOnly date)
    {
        try
        {
            date = new DateOnly(year, month, day);
            return date <= DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private static int Age(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static string CleanList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return string.Join(", ", value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "image/jpeg"
    };

    private static string? FirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }
}
