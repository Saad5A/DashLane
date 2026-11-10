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
    private readonly IdVerifier _idVerifier;

    public HomeController(
        ILogger<HomeController> logger,
        MembershipService membership,
        GoogleAuthOptions google,
        OtpSender otp,
        IdVerifier idVerifier)
    {
        _logger = logger;
        _membership = membership;
        _google = google;
        _otp = otp;
        _idVerifier = idVerifier;
    }

    public async Task<IActionResult> Index()
    {
        var stats = await _membership.NetworkStatsAsync();
        string? continueAction = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var member = await CurrentMember();
            var open = FirstOpenStep(member);
            continueAction = member.CompletedSteps == 0 && open == 1 ? nameof(Community) : SetupSteps[open - 1].Action;
        }

        return View(new HomeIndexViewModel
        {
            ContinueAction = continueAction,
            MemberCount = stats.Members,
            PhoneVerifiedCount = stats.PhoneVerified,
            DocumentsSubmittedCount = stats.DocumentsSubmitted,
            CompletedCount = stats.Completed
        });
    }

    [HttpGet]
    public async Task<IActionResult> SignIn()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await ResumeSetup();
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
            ViewData["AuthNotice"] = Text.T("The email or password is incorrect.");
            ViewData["Email"] = email;
            ViewData["GoogleEnabled"] = _google.IsConfigured;
            return View("SignIn");
        }

        await SignInMember(member, remember);
        return ResumeSetup(member);
    }

    [HttpGet]
    public async Task<IActionResult> SignUp()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return await ResumeSetup();
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
            ViewData["AuthNotice"] = Text.T("Agree to the Terms of Service and Privacy Policy to continue.");
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
        return ResumeSetup(member);
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
            ViewData["AuthNotice"] = Text.T("Agree to the Terms of Service and Privacy Policy to continue with Google.");
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
            ViewData["AuthNotice"] = Text.T("Google sign-in was cancelled or failed. Try again.");
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
            ViewData["AuthNotice"] = error ?? Text.T("Google sign-in could not create an account.");
            ViewData["GoogleEnabled"] = true;
            return View("SignIn");
        }

        await SignInMember(member, remember: true);
        return ResumeSetup(member);
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
        ViewData["AuthNotice"] = Text.T("Google sign-in is not configured.");
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
            ViewData["Notice"] = Text.T("Enter your full legal name.");
            return View(member);
        }

        if (dobDay is null || dobMonth is null || dobYear is null ||
            !TryDate(dobYear.Value, dobMonth.Value, dobDay.Value, out var dob))
        {
            ViewData["Notice"] = Text.T("Enter a real date of birth.");
            return View(member);
        }

        var age = Age(dob);
        if (age < 18)
        {
            ViewData["Notice"] = Text.T("You must be at least 18 years old.");
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
    public async Task<IActionResult> Profile()
    {
        var member = await CurrentMember();
        return Gate(member, 2) ?? View(member);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string? bio, string? languages, string? preferences, IFormFile? photo, bool removePhoto = false)
    {
        var member = await CurrentMember();
        if (Gate(member, 2) is { } blockedProfile)
        {
            return blockedProfile;
        }

        if (removePhoto)
        {
            _membership.ClearPhoto(member);
            await _membership.SaveAsync(member);
            ViewData["Notice"] = Text.T("Photo removed.");
            ViewData["NoticeKind"] = "ok";
            return View(member);
        }

        bio = (bio ?? "").Trim();
        if (string.IsNullOrWhiteSpace(bio))
        {
            ViewData["Notice"] = Text.T("Add a short bio before continuing.");
            return View(member);
        }

        if (bio.Length > 300)
        {
            ViewData["Notice"] = Text.T("Keep the bio to 300 characters.");
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
        var member = await CurrentMember();
        ViewData["SmsReady"] = _otp.IsConfigured;
        return Gate(member, 3) ?? View(member);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string? phoneNumber, string? code, string intent)
    {
        var member = await CurrentMember();
        if (Gate(member, 3) is { } blockedContact)
        {
            return blockedContact;
        }

        ViewData["SmsReady"] = _otp.IsConfigured;
        var normalized = MembershipService.NormalizeCanadianPhone(phoneNumber);
        if (normalized == null)
        {
            ViewData["Notice"] = Text.T("Enter a valid 10-digit Canadian mobile number.");
            return View(member);
        }

        member.PhoneNumber = normalized;
        if (intent == "send")
        {
            try
            {
                ViewData["Notice"] = await _otp.SendAsync(member, HttpContext.RequestAborted);
                ViewData["NoticeKind"] = "ok";
                member.PhoneVerified = false;
                member.PhoneCodeHash = null;
                member.PhoneCodeExpiresUtc = DateTime.UtcNow.AddMinutes(10);
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(exception, "OTP delivery rejected for member {MemberId}", member.Id);
                ViewData["Notice"] = exception.Message;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(exception, "OTP delivery failed for member {MemberId}", member.Id);
                ViewData["Notice"] = Text.T("We could not send the SMS verification code. Try again in a moment.");
            }

            await _membership.SaveAsync(member);
            return View(member);
        }

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6 || !code.Trim().All(char.IsDigit))
        {
            ViewData["Notice"] = Text.T("Enter the 6-digit code we sent you.");
            return View(member);
        }

        OtpCheck result;
        try
        {
            result = await _otp.CheckAsync(member, code, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "OTP check failed for member {MemberId}", member.Id);
            ViewData["Notice"] = exception is InvalidOperationException ? exception.Message : Text.T("We couldn't check the code right now. Try again in a moment.");
            return View(member);
        }

        if (result != OtpCheck.Approved)
        {
            if (result != OtpCheck.Wrong)
            {
                member.PhoneCodeExpiresUtc = null;
                await _membership.SaveAsync(member);
            }

            ViewData["Notice"] = result switch
            {
                OtpCheck.Expired => Text.T("That code has expired. Send a new one."),
                OtpCheck.TooManyAttempts => Text.T("Too many wrong tries. Send a new code."),
                _ => Text.T("That code does not match. Send a new code or try again.")
            };
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
    public async Task<IActionResult> Identity()
    {
        var member = await CurrentMember();
        return Gate(member, 4) ?? View(member);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Identity(string? documentType, IFormFile? document, bool removeDocument = false)
    {
        var member = await CurrentMember();
        if (Gate(member, 4) is { } blockedIdentity)
        {
            return blockedIdentity;
        }

        var allowed = new[] { "Driver's Licence", "Passport", "Provincial Photo ID" };
        if (removeDocument)
        {
            _membership.ClearDocument(member);
            member.IssuingCountry = "Canada";
            await _membership.SaveAsync(member);
            ViewData["Notice"] = Text.T("Document removed.");
            ViewData["NoticeKind"] = "ok";
            return View(member);
        }

        if (string.IsNullOrWhiteSpace(documentType) || !allowed.Contains(documentType))
        {
            ViewData["Notice"] = Text.T("Choose the document you are uploading.");
            return View(member);
        }

        if ((document == null || document.Length == 0) && string.IsNullOrWhiteSpace(member.DocumentFileName))
        {
            ViewData["Notice"] = Text.T("Upload a photo or PDF of the document.");
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

        var documentChanged = (document != null && document.Length > 0) || member.DocumentType != documentType;
        member.DocumentType = documentType;
        member.IssuingCountry = "Canada";
        member.DocumentSubmittedUtc = DateTime.UtcNow;

        if (documentChanged || member.DocumentCheckedUtc == null)
        {
            var path = _membership.MemberFilePath(member, member.DocumentFileName);
            var check = path == null
                ? new IdCheckResult(null, "The automatic check couldn't run. Your document will be reviewed manually.", null)
                : await _idVerifier.CheckAsync(member, path, documentType, HttpContext.RequestAborted);
            member.DocumentCheckDecision = check.Decision;
            member.DocumentCheckSummary = check.Summary;
            member.DocumentCheckId = check.TransactionId;
            member.DocumentCheckedUtc = check.Ran ? DateTime.UtcNow : null;
            await _membership.SaveAsync(member);

            if (check.Decision == "reject")
            {
                ViewData["Notice"] = Text.CheckSummary(check.Summary) + " " + Text.T("Upload a clearer photo of a valid, unexpired document.");
                return View(member);
            }
        }
        else
        {
            await _membership.SaveAsync(member);
        }

        return RedirectToAction(nameof(Review));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Review()
    {
        var member = await CurrentMember();
        return Gate(member, 5) ?? View(member);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewConfirm()
    {
        var member = await CurrentMember();
        if (Gate(member, 5) is { } blockedReview)
        {
            return blockedReview;
        }

        if (member.CompletedSteps < 4)
        {
            ViewData["Notice"] = Text.T("Finish each onboarding step before completing setup.");
            return View("Review", member);
        }

        member.OnboardingCompletedUtc = DateTime.UtcNow;
        await _membership.SaveAsync(member);
        ViewData["Notice"] = Text.T("Your profile is saved. The ride feed is not available yet.");
        ViewData["NoticeKind"] = "ok";
        return View("Review", member);
    }

    [Authorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Document()
    {
        var member = await CurrentMember();
        var path = _membership.MemberFilePath(member, member.DocumentFileName);
        return path == null ? NotFound() : PhysicalFile(path, ContentType(path));
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

    private static readonly (string Action, string Label)[] SetupSteps =
    {
        (nameof(AboutYou), "About you"),
        (nameof(Profile), "Profile"),
        (nameof(Contact), "Phone"),
        (nameof(Identity), "Identity"),
        (nameof(Review), "Review"),
    };

    /// <summary>The first setup step (1–5) the member hasn't finished. Later steps stay locked until it's done.</summary>
    public static int FirstOpenStep(Member member)
    {
        if (!member.DateOfBirth.HasValue || string.IsNullOrWhiteSpace(member.LegalName)) return 1;
        if (!member.ProfileCompletedUtc.HasValue) return 2;
        if (!member.PhoneVerified) return 3;
        if (string.IsNullOrWhiteSpace(member.DocumentFileName)) return 4;
        return 5;
    }

    private IActionResult? Gate(Member member, int step)
    {
        var open = FirstOpenStep(member);
        if (step <= open)
        {
            return null;
        }

        TempData["GateNotice"] = Text.T("Finish “{0}” before moving on.", Text.T(SetupSteps[open - 1].Label));
        return RedirectToAction(SetupSteps[open - 1].Action);
    }

    private IActionResult ResumeSetup(Member member)
    {
        var open = FirstOpenStep(member);
        if (open == 5)
        {
            return RedirectToAction(nameof(Review));
        }

        return member.CompletedSteps == 0 && open == 1
            ? RedirectToAction(nameof(Community))
            : RedirectToAction(SetupSteps[open - 1].Action);
    }

    private async Task<IActionResult> ResumeSetup() => ResumeSetup(await CurrentMember());

    public override async Task OnActionExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        // A sign-in cookie can outlive its member row (e.g. after a database reset). Sign out instead of crashing.
        if (User.Identity?.IsAuthenticated == true &&
            (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || await _membership.FindAsync(id) == null))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Result = RedirectToAction(nameof(SignIn));
            return;
        }

        await next();
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
