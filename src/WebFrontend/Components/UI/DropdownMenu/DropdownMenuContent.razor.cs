using Microsoft.AspNetCore.Components;
using WebFrontend.Components.UI.Shared;

namespace WebFrontend.Components.UI.DropdownMenu;

public partial class DropdownMenuContent : ComponentBase
{
    [CascadingParameter]
    public DropdownMenu? ParentMenu { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public int SideOffset { get; set; } = 4;

    [Parameter]
    public string? Class { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string GetClass()
    {
        var baseClasses =
            "bg-dropdown text-dropdown-foreground z-50 max-h-[--radix-dropdown-menu-content-available-height] " +
            "min-w-[8rem] overflow-x-hidden overflow-y-auto rounded-md border border-border p-1 shadow-md";

        return ClassBuilder.Merge(baseClasses, Class);
    }
}
