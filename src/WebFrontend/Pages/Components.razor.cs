using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using WebFrontend.Components.UI.Select;
using WebFrontend.Services;

namespace WebFrontend.Pages;

public partial class Components : ComponentBase
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private ScrollLockService ScrollLockService { get; set; } = default!;
    [Inject] private ToastService ToastSvc { get; set; } = default!;

    private bool _switchChecked = false;
    private bool _switchChecked2 = false;
    private bool _switchChecked3 = false;
    private bool _checkboxChecked = false;
    private bool _checkboxChecked2 = false;
    private bool _checkboxChecked3 = false;
    private bool _toggleButtonPrimary = false;
    private bool _toggleButtonSecondary = false;
    public bool DialogOpen = false;
    private bool _dropdownOpen = false;
    private const string Username = "shadcn";
    private bool _scrollLocked = false;

    private string _activeDashboardTab = "overview";
    private string? _selectedClinic;
    private string? _symptomNotes;
    private DateOnly? _appointmentDate;

    private int _progressValue = 40;

    private bool _sheetOpen;

    private readonly IReadOnlyList<SelectOption> _clinicOptions = new[]
    {
        new SelectOption("downtown", "Downtown Health Center"),
        new SelectOption("north", "Northside Clinic"),
        new SelectOption("telehealth", "Telehealth Only")
    };

    private string _selectedClinicDisplay => _selectedClinic switch
    {
        "downtown" => "Downtown Health Center",
        "north" => "Northside Clinic",
        "telehealth" => "Telehealth Only",
        _ => _selectedClinic ?? "None"
    };

    private async Task ToggleScroll()
    {
        _scrollLocked = !_scrollLocked;
        if (_scrollLocked)
            await ScrollLockService.LockAsync();
        else
            await ScrollLockService.UnlockAsync();
    }

    private async Task OnDropdownOpenChanged(bool isOpen)
    {
        _dropdownOpen = isOpen;
        if (isOpen)
            await ScrollLockService.LockAsync();
        else
            await ScrollLockService.UnlockAsync();
    }

    private void OnSwitchCheckedChanged(bool value) => _switchChecked = value;
    private void OnSwitch2CheckedChanged(bool value) => _switchChecked2 = value;
    private void OnSwitch3CheckedChanged(bool value) => _switchChecked3 = value;
    private void OnCheckboxCheckedChanged(bool value) => _checkboxChecked = value;
    private void OnCheckbox2CheckedChanged(bool value) => _checkboxChecked2 = value;
    private void OnCheckbox3CheckedChanged(bool value) => _checkboxChecked3 = value;

    private void StepProgress()
    {
        _progressValue += 15;
        if (_progressValue > 100)
        {
            _progressValue = 0;
        }
    }

    private Task OnClinicChanged(string? value)
    {
        _selectedClinic = value;
        return Task.CompletedTask;
    }

    private async Task OnSheetOpenChanged(bool isOpen)
    {
        _sheetOpen = isOpen;
        if (isOpen)
            await ScrollLockService.LockAsync();
        else
            await ScrollLockService.UnlockAsync();
    }

    private void ShowToast(ToastType type)
    {
        switch (type)
        {
            case ToastType.Success:
                ToastSvc.ShowSuccess("Appointment booked", "We sent a confirmation to your email.");
                break;
            case ToastType.Error:
                ToastSvc.ShowError("Booking failed", "Something went wrong while scheduling your visit.");
                break;
            case ToastType.Info:
                ToastSvc.ShowInfo("Reminder set", "We will remind you 24 hours before your appointment.");
                break;
            default:
                ToastSvc.Show("Notification");
                break;
        }
    }

    private void NavigateDashboard() { }
    private void NavigateAppointments() { }
    private void NavigateHistory() { }
    private void NavigateProfile() { }
    private void NavigateBilling() { }
    private void NavigateSettings() { }
}
