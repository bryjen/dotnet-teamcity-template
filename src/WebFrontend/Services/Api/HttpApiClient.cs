using System.Net.Http.Json;
using System.Net.Http.Headers;
using WebFrontend.Services.Auth;

namespace WebFrontend.Services.Api;

public sealed class HttpApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenProvider _tokenProvider;
    private readonly BackendStatus _backendStatus;

    public HttpApiClient(ApiHttpClient apiHttpClient, ITokenProvider tokenProvider, BackendStatus backendStatus)
    {
        _http = apiHttpClient.Client;
        _tokenProvider = tokenProvider;
        _backendStatus = backendStatus;
    }

    public async Task<ApiResult<TResponse>> GetAsync<TResponse>(string path, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            await AttachAuthHeaderAsync(request);
            using var response = await _http.SendAsync(request, ct);
            var result = await ReadAsResult<TResponse>(response, ct);
            NotifyBackendStatus(result);
            return result;
        }
        catch (HttpRequestException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Backend unreachable: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Backend unreachable");
            return result;
        }
        catch (TaskCanceledException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Request cancelled/timed out: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Request cancelled/timed out");
            return result;
        }
    }

    public async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            };
            await AttachAuthHeaderAsync(request);
            using var response = await _http.SendAsync(request, ct);
            var result = await ReadAsResult<TResponse>(response, ct);
            NotifyBackendStatus(result);
            return result;
        }
        catch (HttpRequestException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Backend unreachable: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Backend unreachable");
            return result;
        }
        catch (TaskCanceledException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Request cancelled/timed out: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Request cancelled/timed out");
            return result;
        }
    }

    public async Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(body)
            };
            await AttachAuthHeaderAsync(request);
            using var response = await _http.SendAsync(request, ct);
            var result = await ReadAsResult<TResponse>(response, ct);
            NotifyBackendStatus(result);
            return result;
        }
        catch (HttpRequestException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Backend unreachable: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Backend unreachable");
            return result;
        }
        catch (TaskCanceledException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Request cancelled/timed out: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Request cancelled/timed out");
            return result;
        }
    }

    public async Task<ApiResult<TResponse>> PatchAsync<TResponse>(string path, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, path);
            await AttachAuthHeaderAsync(request);
            using var response = await _http.SendAsync(request, ct);
            var result = await ReadAsResult<TResponse>(response, ct);
            NotifyBackendStatus(result);
            return result;
        }
        catch (HttpRequestException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Backend unreachable: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Backend unreachable");
            return result;
        }
        catch (TaskCanceledException ex)
        {
            var result = ApiResult<TResponse>.Failure($"Request cancelled/timed out: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Request cancelled/timed out");
            return result;
        }
    }

    public async Task<ApiResult<bool>> DeleteAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, path);
            await AttachAuthHeaderAsync(request);
            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var ok = ApiResult<bool>.Success(true, response.StatusCode);
                _backendStatus.Clear();
                return ok;
            }
            var fail = ApiResult<bool>.Failure(await ReadErrorMessage(response, ct), response.StatusCode);
            _backendStatus.SetError(fail.ErrorMessage ?? "Request failed");
            return fail;
        }
        catch (HttpRequestException ex)
        {
            var result = ApiResult<bool>.Failure($"Backend unreachable: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Backend unreachable");
            return result;
        }
        catch (TaskCanceledException ex)
        {
            var result = ApiResult<bool>.Failure($"Request cancelled/timed out: {ex.Message}");
            _backendStatus.SetError(result.ErrorMessage ?? "Request cancelled/timed out");
            return result;
        }
    }

    private static async Task<ApiResult<T>> ReadAsResult<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            if (value == null)
            {
                return ApiResult<T>.Failure("Empty response body", response.StatusCode);
            }
            return ApiResult<T>.Success(value, response.StatusCode);
        }

        return ApiResult<T>.Failure(await ReadErrorMessage(response, ct), response.StatusCode);
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }
        catch
        {
            // ignore
        }

        return $"Request failed ({(int)response.StatusCode} {response.StatusCode})";
    }

    private async Task AttachAuthHeaderAsync(HttpRequestMessage request)
    {
        var token = await _tokenProvider.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private void NotifyBackendStatus<T>(ApiResult<T> result)
    {
        if (result.IsSuccess)
        {
            _backendStatus.Clear();
        }
        else
        {
            _backendStatus.SetError(result.ErrorMessage ?? "Request failed");
        }
    }
}


