using WebApi.DTOs.Tags;

namespace WebFrontend.Services.Api;

public sealed class HttpTagsApi : ITagsApi
{
    private readonly HttpApiClient _api;

    public HttpTagsApi(HttpApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<List<TagDto>>> GetAllAsync(CancellationToken ct = default)
        => _api.GetAsync<List<TagDto>>("/api/v1/tags", ct);

    public Task<ApiResult<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync<TagDto>($"/api/v1/tags/{id}", ct);

    public Task<ApiResult<TagDto>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
        => _api.PostAsync<CreateTagRequest, TagDto>("/api/v1/tags", request, ct);

    public Task<ApiResult<TagDto>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
        => _api.PutAsync<UpdateTagRequest, TagDto>($"/api/v1/tags/{id}", request, ct);

    public Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
        => _api.DeleteAsync($"/api/v1/tags/{id}", ct);
}


