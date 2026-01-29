using System.Text.Json;
using Web.Common.DTOs.Auth;
using WebApi.ApiWrapper.Exceptions;
using WebApi.ApiWrapper.Services;

namespace WebFrontend.Services;

/// <summary>
/// Service for handling authentication operations.
/// </summary>
public class AuthService
{
    private readonly IAuthApiClient _authApiClient;
    private readonly LocalStorageTokenProvider _tokenProvider;
    private UserDto? _currentUser;

    public AuthService(IAuthApiClient authApiClient, ITokenProvider tokenProvider)
    {
        _authApiClient = authApiClient;
        _tokenProvider = tokenProvider as LocalStorageTokenProvider 
            ?? throw new ArgumentException("TokenProvider must be LocalStorageTokenProvider", nameof(tokenProvider));
    }

    public UserDto? CurrentUser => _currentUser;

    public async Task<bool> RegisterAsync(string email, string password)
    {
        try
        {
            var request = new RegisterRequest
            {
                Email = email,
                Password = password
            };

            var response = await _authApiClient.RegisterAsync(request);
            await StoreAuthResponseAsync(response);
            return true;
        }
        catch (ApiValidationException ex)
        {
            throw new Exception(ex.Message);
        }
        catch (ApiConflictException ex)
        {
            throw new Exception(ex.Message);
        }
        catch (ApiException ex)
        {
            throw new Exception($"Registration failed: {ex.Message}");
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var response = await _authApiClient.LoginAsync(request);
            await StoreAuthResponseAsync(response);
            return true;
        }
        catch (ApiUnauthorizedException)
        {
            throw new Exception("Invalid email or password");
        }
        catch (ApiException ex)
        {
            throw new Exception($"Login failed: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        _tokenProvider.ClearToken();
        await _tokenProvider.SetRefreshTokenAsync(null);
        await _tokenProvider.SetUserAsync(null);
    }

    /// <summary>
    /// Clears authentication tokens without full logout (used when refresh fails).
    /// </summary>
    public async Task ClearTokensAsync()
    {
        _currentUser = null;
        _tokenProvider.ClearToken();
        await _tokenProvider.SetRefreshTokenAsync(null);
        await _tokenProvider.SetUserAsync(null);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _tokenProvider.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // Optionally validate token by getting current user
        try
        {
            await LoadCurrentUserAsync();
            return _currentUser != null;
        }
        catch
        {
            // Token might be invalid, clear it
            await LogoutAsync();
            return false;
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
            return _currentUser;

        await LoadCurrentUserAsync();
        return _currentUser;
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            // Try to load from localStorage first
            var userJson = await _tokenProvider.GetUserAsync();
            if (!string.IsNullOrWhiteSpace(userJson))
            {
                _currentUser = JsonSerializer.Deserialize<UserDto>(userJson);
                if (_currentUser != null)
                    return;
            }

            // If not in localStorage, fetch from API
            _currentUser = await _authApiClient.GetCurrentUserAsync();
            if (_currentUser != null)
            {
                await _tokenProvider.SetUserAsync(JsonSerializer.Serialize(_currentUser));
            }
        }
        catch
        {
            _currentUser = null;
            throw;
        }
    }

    private async Task StoreAuthResponseAsync(AuthResponse response)
    {
        _tokenProvider.SetToken(response.AccessToken);
        await _tokenProvider.SetRefreshTokenAsync(response.RefreshToken);
        _currentUser = response.User;
        await _tokenProvider.SetUserAsync(JsonSerializer.Serialize(response.User));
    }
}
