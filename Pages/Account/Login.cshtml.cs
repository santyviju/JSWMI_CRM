using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;

        public LoginModel(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        [BindProperty]
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; } = true;

        public string? ErrorMessage { get; set; }

        public string? ReturnUrl { get; set; }

        public IActionResult OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            // If already signed in, redirect directly to dashboard or return URL
            if (User.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                return RedirectToPage("/Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var (success, userId, userEmail, error) = await _supabaseService.AuthenticateUserAsync(Email.Trim(), Password);

            if (!success)
            {
                ErrorMessage = error ?? "Invalid email or password. Please verify your credentials.";
                return Page();
            }

            var displayName = userEmail?.Split('@')[0] ?? "JSW User";
            if (displayName.Length > 0)
            {
                displayName = char.ToUpperInvariant(displayName[0]) + displayName[1..];
            }

            // Retrieve or initialize user profile for Role & Location assignment
            var userProfile = !string.IsNullOrEmpty(userId) ? await _supabaseService.GetUserProfileAsync(userId) : null;
            if (userProfile == null && !string.IsNullOrEmpty(userId))
            {
                var locations = await _supabaseService.GetLocationsAsync();
                var defaultLoc = locations.FirstOrDefault(l => l.IsActive) ?? locations.FirstOrDefault();

                // If email contains admin or starts with santosh/santy, set as Admin, otherwise Planner
                var lowerEmail = (userEmail ?? Email).Trim().ToLowerInvariant();
                var assignedRole = (lowerEmail.Contains("admin") || lowerEmail.StartsWith("santosh") || lowerEmail.StartsWith("santy"))
                    ? "Admin"
                    : "Planner";

                userProfile = new JSWMI_CRM.Models.UserProfile
                {
                    UserId = userId,
                    Email = userEmail ?? Email,
                    FullName = displayName,
                    Role = assignedRole,
                    LocationId = defaultLoc?.Id,
                    LocationName = defaultLoc?.Name
                };

                await _supabaseService.SaveUserProfileAsync(userProfile);
            }

            var role = userProfile?.Role ?? "Planner";
            var locationId = userProfile?.LocationId ?? string.Empty;
            var locationName = userProfile?.LocationName ?? "All Locations";
            var fullName = !string.IsNullOrWhiteSpace(userProfile?.FullName) ? userProfile.FullName : displayName;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId ?? Guid.NewGuid().ToString()),
                new(ClaimTypes.Name, fullName),
                new(ClaimTypes.Email, userEmail ?? Email),
                new(ClaimTypes.Role, role),
                new("LocationId", locationId),
                new("LocationName", locationName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToPage("/Index");
        }
    }
}
