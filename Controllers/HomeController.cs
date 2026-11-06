using System.Diagnostics;
using System.Security.Claims;
using DashLane.Models;
using DashLane.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DashLane.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MembershipService _membership;

    public HomeController(ILogger<HomeController> logger, MembershipService membership)
    {
        _logger = logger;
        _membership = membership;
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
            return View("SignUp");
        }

        var (member, error) = await _membership.RegisterAsync(fullName ?? "", email ?? "", password ?? "");
        if (member == null)
        {
            ViewData["AuthNotice"] = error;
            ViewData["FullName"] = fullName;
            ViewData["Email"] = email;
            return View("SignUp");
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
    public async Task<IActionResult> Profile(string? bio, string? languages, string? preferences, IFormFile? photo)
    {
        var member = await CurrentMember();
        bio = (bio ?? "").Trim();
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

        member.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio;
        member.Languages = CleanList(languages);
        member.Preferences = CleanList(preferences);
        member.ProfileCompletedUtc = DateTime.UtcNow;
        await _membership.SaveAsync(member);
        return RedirectToAction(nameof(Contact));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Contact() => View(await CurrentMember());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string? phoneNumber, string? code, string intent)
    {
        var member = await CurrentMember();
        var digits = MembershipService.Digits(phoneNumber);
        if (digits.Length < 10 || digits.Length > 15)
        {
            ViewData["Notice"] = "Enter a phone number with the country code.";
            return View(member);
        }

        member.PhoneNumber = "+" + digits;
        if (intent == "send")
        {
            var issued = _membership.CreatePhoneCode(member);
            await _membership.SaveAsync(member);
            TempData["VerificationCode"] = issued;
            ViewData["Notice"] = "No SMS provider is configured. Your verification code is " + issued + ". It expires in 10 minutes.";
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
    public async Task<IActionResult> Identity(string? documentType, string? issuingCountry, IFormFile? document)
    {
        var member = await CurrentMember();
        var allowed = new[] { "Driver's Licence", "Passport", "Provincial Photo ID" };
        if (string.IsNullOrWhiteSpace(documentType) || !allowed.Contains(documentType))
        {
            ViewData["Notice"] = "Choose the document you are uploading.";
            return View(member);
        }

        issuingCountry = (issuingCountry ?? "").Trim();
        if (issuingCountry.Length < 2)
        {
            ViewData["Notice"] = "Enter the country that issued the document.";
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
        member.IssuingCountry = issuingCountry;
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
}
