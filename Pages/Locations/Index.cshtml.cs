using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JSWMI_CRM.Models;
using JSWMI_CRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JSWMI_CRM.Pages.Locations
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ISupabaseService _supabaseService;

        public IndexModel(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public List<Location> Locations { get; set; } = new();
        public List<UserProfile> UserProfiles { get; set; } = new();

        [BindProperty]
        public Location NewLocation { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool IsAdmin => User.IsInRole("Admin");

        public async Task<IActionResult> OnGetAsync()
        {
            Locations = await _supabaseService.GetLocationsAsync();
            if (IsAdmin)
            {
                UserProfiles = await _supabaseService.GetAllUserProfilesAsync();
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSaveLocationAsync()
        {
            if (!IsAdmin)
            {
                ErrorMessage = "Only administrators can add or update branch locations.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(NewLocation.Code) || string.IsNullOrWhiteSpace(NewLocation.Name))
            {
                ErrorMessage = "Branch Code and Branch Name are required.";
                return RedirectToPage();
            }

            if (string.IsNullOrEmpty(NewLocation.Id))
            {
                NewLocation.Id = "LOC-" + NewLocation.Code.Trim().ToUpperInvariant();
            }

            NewLocation.Code = NewLocation.Code.Trim().ToUpperInvariant();
            NewLocation.Name = NewLocation.Name.Trim();
            NewLocation.City = NewLocation.City?.Trim();
            NewLocation.State = NewLocation.State?.Trim();

            var saved = await _supabaseService.SaveLocationAsync(NewLocation);
            if (saved != null)
            {
                SuccessMessage = $"Branch location '{NewLocation.Name}' ({NewLocation.Code}) saved successfully.";
            }
            else
            {
                ErrorMessage = "Failed to save branch location to the database.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(string id)
        {
            if (!IsAdmin)
            {
                ErrorMessage = "Only administrators can toggle branch status.";
                return RedirectToPage();
            }

            var loc = await _supabaseService.GetLocationByIdAsync(id);
            if (loc != null)
            {
                loc.IsActive = !loc.IsActive;
                await _supabaseService.SaveLocationAsync(loc);
                SuccessMessage = $"Branch '{loc.Name}' is now {(loc.IsActive ? "Active" : "Inactive")}.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAssignUserAsync(string userId, string locationId, string role)
        {
            if (!IsAdmin)
            {
                ErrorMessage = "Only administrators can assign users to branches and roles.";
                return RedirectToPage();
            }

            var profile = await _supabaseService.GetUserProfileAsync(userId);
            if (profile != null)
            {
                var loc = await _supabaseService.GetLocationByIdAsync(locationId);
                profile.LocationId = loc?.Id;
                profile.LocationName = loc?.Name;
                profile.Role = role;
                profile.UpdatedAt = DateTime.UtcNow;

                await _supabaseService.SaveUserProfileAsync(profile);
                SuccessMessage = $"User '{profile.FullName}' updated with Role '{role}' and Location '{loc?.Name ?? "All Locations"}'.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteLocationAsync(string id)
        {
            if (!IsAdmin)
            {
                ErrorMessage = "Only administrators can delete branch locations.";
                return RedirectToPage();
            }

            var success = await _supabaseService.DeleteLocationAsync(id);
            if (success)
            {
                SuccessMessage = "Branch location deleted successfully.";
            }
            else
            {
                ErrorMessage = "Failed to delete location. It may be in use by sales orders or cutting plans.";
            }

            return RedirectToPage();
        }
    }
}
