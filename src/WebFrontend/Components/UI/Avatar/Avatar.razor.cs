namespace WebFrontend.Components.UI.Avatar;

public partial class Avatar
{
    public bool ShowFallback { get; set; } = false;
    private string? _imageSrc;

    public void OnImageError()
    {
        ShowFallback = true;
        StateHasChanged();
    }

    public string? GetImageSrc() => _imageSrc;

    public void SetImageSrc(string? src)
    {
        _imageSrc = src;
        ShowFallback = false;
    }
}
