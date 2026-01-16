using WebApi.DTOs.Tags;

namespace WebApi.Services.Tag;

public interface ITagService
{
    Task<List<TagDto>> GetAllTagsAsync(Guid userId);
    Task<TagDto?> GetTagByIdAsync(Guid tagId, Guid userId);
    Task<TagDto> CreateTagAsync(CreateTagRequest request, Guid userId);
    Task<TagDto?> UpdateTagAsync(Guid tagId, UpdateTagRequest request, Guid userId);
    Task<bool> DeleteTagAsync(Guid tagId, Guid userId);
}
