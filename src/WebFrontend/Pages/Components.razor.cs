using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebFrontend.Services;

namespace WebFrontend.Pages;

public partial class Components : ComponentBase
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private ScrollLockService ScrollLockService { get; set; } = default!;

    private bool _switchChecked = false;
    private bool _switchChecked2 = false;
    private bool _checkboxChecked = false;
    private bool _checkboxChecked2 = false;
    public bool DialogOpen = false;
    private bool _dropdownOpen = false;
    private const string Username = "shadcn";
    private bool _scrollLocked = false;

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
    private void OnCheckboxCheckedChanged(bool value) => _checkboxChecked = value;
    private void OnCheckbox2CheckedChanged(bool value) => _checkboxChecked2 = value;
}
