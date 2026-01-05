namespace WebFrontend.Services.Api;

public sealed class BackendStatus
{
    public event Action? Changed;

    public string? LastError { get; private set; }

    public void SetError(string message)
    {
        LastError = message;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (LastError == null) return;
        LastError = null;
        Changed?.Invoke();
    }
}


