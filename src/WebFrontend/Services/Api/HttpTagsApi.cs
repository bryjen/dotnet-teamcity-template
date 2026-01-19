using WebApi.DTOs.Tags;
using WebFrontend.Models;

namespace WebFrontend.Services.Api;

public sealed class HttpTagsApi(
    HttpApiClient api)
{
    public Task<ApiResult<List<TagDto>>> GetAllAsync(CancellationToken ct = default)
        => api.GetAsync<List<TagDto>>("/api/v1/tags", ct);

    public Task<ApiResult<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        => api.GetAsync<TagDto>($"/api/v1/tags/{id}", ct);

    public Task<ApiResult<TagDto>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
        => api.PostAsync<CreateTagRequest, TagDto>("/api/v1/tags", request, ct);

    public Task<ApiResult<TagDto>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
        => api.PutAsync<UpdateTagRequest, TagDto>($"/api/v1/tags/{id}", request, ct);

    public Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
        => api.DeleteAsync($"/api/v1/tags/{id}", ct);
}


