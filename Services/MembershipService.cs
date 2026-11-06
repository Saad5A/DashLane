using System.Security.Cryptography;
using DashLane.Data;
using DashLane.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Services;

public class MembershipService
{
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] DocumentExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
    private readonly DashLaneContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly PasswordHasher<Member> _hasher = new();

    public MembershipService(DashLaneContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public Task<int> CountAsync() => _db.Members.CountAsync();

    public Task<Member?> FindAsync(int id) => _db.Members.FirstOrDefaultAsync(member => member.Id == id);

    public Task<Member?> FindByEmailAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        return _db.Members.FirstOrDefaultAsync(member => member.Email == normalized);
    }

    public async Task<(Member? Member, string? Error)> RegisterAsync(string fullName, string email, string password)
    {
        fullName = fullName.Trim();
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (null, "Enter your full name.");
        }

        if (!IsValidEmail(normalized))
        {
            return (null, "Enter a valid email address.");
        }

        if (!IsValidPassword(password))
        {
            return (null, "Use at least 8 characters, one number, and one special character.");
        }

        if (await _db.Members.AnyAsync(member => member.Email == normalized))
        {
            return (null, "An account with this email already exists.");
        }

        var member = new Member
        {
            Email = normalized,
            FullName = fullName,
            LegalName = fullName,
            CreatedAtUtc = DateTime.UtcNow
        };
        member.PasswordHash = _hasher.HashPassword(member, password);
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return (member, null);
    }

    public async Task<Member?> AuthenticateAsync(string email, string password)
    {
        var member = await FindByEmailAsync(email);
        if (member == null || string.IsNullOrEmpty(member.PasswordHash))
        {
            return null;
        }

        var result = _hasher.VerifyHashedPassword(member, member.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : member;
    }

    public async Task<(Member? Member, string? Error)> FindOrCreateGoogleAsync(string subjectId, string email, string fullName)
    {
        subjectId = subjectId.Trim();
        fullName = string.IsNullOrWhiteSpace(fullName) ? "Google member" : fullName.Trim();
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(subjectId) || !IsValidEmail(normalized))
        {
            return (null, "Google did not return a usable account email.");
        }

        var bySubject = await _db.Members.FirstOrDefaultAsync(member => member.GoogleSubjectId == subjectId);
        if (bySubject != null)
        {
            if (!string.Equals(bySubject.Email, normalized, StringComparison.Ordinal))
            {
                bySubject.Email = normalized;
            }

            if (!string.IsNullOrWhiteSpace(fullName) && bySubject.FullName != fullName)
            {
                bySubject.FullName = fullName;
                if (string.IsNullOrWhiteSpace(bySubject.LegalName))
                {
                    bySubject.LegalName = fullName;
                }
            }

            await _db.SaveChangesAsync();
            return (bySubject, null);
        }

        var byEmail = await FindByEmailAsync(normalized);
        if (byEmail != null)
        {
            byEmail.GoogleSubjectId = subjectId;
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                byEmail.FullName = fullName;
                if (string.IsNullOrWhiteSpace(byEmail.LegalName))
                {
                    byEmail.LegalName = fullName;
                }
            }

            await _db.SaveChangesAsync();
            return (byEmail, null);
        }

        var member = new Member
        {
            Email = normalized,
            GoogleSubjectId = subjectId,
            FullName = fullName,
            LegalName = fullName,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return (member, null);
    }

    public async Task SaveAsync(Member member)
    {
        await _db.SaveChangesAsync();
    }

    public string CreatePhoneCode(Member member)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        member.PhoneCodeHash = _hasher.HashPassword(member, code);
        member.PhoneCodeExpiresUtc = DateTime.UtcNow.AddMinutes(10);
        member.PhoneVerified = false;
        return code;
    }

    public bool PhoneCodeMatches(Member member, string code)
    {
        if (string.IsNullOrWhiteSpace(member.PhoneCodeHash) || member.PhoneCodeExpiresUtc == null)
        {
            return false;
        }

        if (member.PhoneCodeExpiresUtc < DateTime.UtcNow)
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(member, member.PhoneCodeHash, code.Trim());
        return result != PasswordVerificationResult.Failed;
    }

    public async Task<string> SaveFileAsync(Member member, IFormFile file, string kind)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = kind == "photo" ? PhotoExtensions : DocumentExtensions;
        if (!allowed.Contains(extension))
        {
            throw new InvalidOperationException(kind == "photo"
                ? "Use a JPG, PNG, or WebP photo."
                : "Use a JPG, PNG, WebP, or PDF document.");
        }

        if (file.Length == 0 || file.Length > 10 * 1024 * 1024)
        {
            throw new InvalidOperationException("The file must be under 10 MB.");
        }

        var directory = MemberDirectory(member.Id);
        Directory.CreateDirectory(directory);
        var fileName = kind + extension;
        var path = Path.Combine(directory, fileName);
        await using var stream = File.Create(path);
        await file.CopyToAsync(stream);
        return fileName;
    }

    public string? MemberFilePath(Member member, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(MemberDirectory(member.Id), fileName));
        var root = Path.GetFullPath(MemberDirectory(member.Id));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return null;
        }

        return path;
    }

    public static bool IsValidPassword(string password) =>
        password.Length >= 8 && password.Any(char.IsDigit) && password.Any(character => !char.IsLetterOrDigit(character));

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Contains(' ') || email.Contains(".."))
        {
            return false;
        }

        try
        {
            var parsed = new System.Net.Mail.MailAddress(email);
            var domain = parsed.Host;
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase) &&
                   domain.Contains('.') &&
                   domain.Split('.').Last().Length >= 2 &&
                   !domain.StartsWith('.') &&
                   !domain.EndsWith('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string Digits(string? value) =>
        new string((value ?? "").Where(char.IsDigit).ToArray());

    public static string? NormalizeCanadianPhone(string? value)
    {
        var digits = Digits(value);
        if (digits.Length == 11 && digits.StartsWith('1'))
        {
            digits = digits[1..];
        }

        return digits.Length == 10 ? "+1" + digits : null;
    }

    public void ClearPhoto(Member member)
    {
        DeleteMemberFile(member, member.PhotoFileName);
        member.PhotoFileName = null;
    }

    public void ClearDocument(Member member)
    {
        DeleteMemberFile(member, member.DocumentFileName);
        member.DocumentFileName = null;
        member.DocumentType = null;
        member.DocumentSubmittedUtc = null;
    }

    private void DeleteMemberFile(Member member, string? fileName)
    {
        var path = MemberFilePath(member, fileName);
        if (path != null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string MemberDirectory(int memberId) =>
        Path.Combine(_environment.ContentRootPath, "App_Data", "members", memberId.ToString());
}
