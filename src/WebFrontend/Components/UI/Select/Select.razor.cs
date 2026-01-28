using Microsoft.AspNetCore.Components;
using WebFrontend.Utils;

namespace WebFrontend.Components.UI.Select;

public record SelectOption(string Value, string Label, bool Disabled = false);

[ComponentMetadata(
    Description = "Custom dropdown-style select component.",
    IsEntry = true,
    Group = nameof(Select))]
public partial class Select
{
}

