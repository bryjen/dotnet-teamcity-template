using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WebFrontend.Services;

namespace WebFrontend.Pages.Settings;

public partial class PersonalInformation : ComponentBase
{
    [Inject] private AuthService AuthService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;
    [Inject] private ToastService ToastService { get; set; } = null!;

    protected PersonalInformationModel Model { get; set; } = new();
    protected bool IsSaving { get; set; } = false;
    protected string? ProfileImageUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadUserDataAsync();
    }

    private async Task LoadUserDataAsync()
    {
        var user = await AuthService.GetCurrentUserAsync();
        if (user != null)
        {
            Model.Email = user.Email;
            // TODO: Load other user data from API when backend is ready
        }
    }

    protected string GetUserInitials()
    {
        var user = AuthService.CurrentUser;
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            var email = user.Email;
            var parts = email.Split('@');
            if (parts.Length > 0 && parts[0].Length > 0)
            {
                return parts[0].Substring(0, 1).ToUpperInvariant();
            }
        }
        return "U";
    }

    protected async Task HandleSave()
    {
        IsSaving = true;

        try
        {
            // TODO: Call API to save user profile when backend is ready
            // For now, just show a success toast
            await Task.Delay(500); // Simulate API call

            ToastService.ShowSuccess("Profile updated", "Your personal information has been saved successfully.");
        }
        catch (Exception ex)
        {
            ToastService.ShowError("Failed to save", ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected async Task HandleRemoveProfileImage()
    {
        ProfileImageUrl = null;
        // TODO: Call API to remove profile image when backend is ready
        await Task.CompletedTask;
    }
}

public class PersonalInformationModel
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Username { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    public string? PhoneCountryCode { get; set; }

    [Phone(ErrorMessage = "Invalid phone number")]
    public string? PhoneNumber { get; set; }

    public string? Country { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }
}
