using WebApi.DTOs.Tags;

namespace WebFrontend.Services.Api;

public interface ITagsApi
{
    Task<ApiResult<List<TagDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResult<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<TagDto>> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<ApiResult<TagDto>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default);
    Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}


