using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.DTOs.Tags;
using WebApi.Models;

namespace WebApi.Services;

public class TagService : ITagService
{
    private readonly AppDbContext _context;

    public TagService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TagDto>> GetAllTagsAsync(Guid userId)
    {
        var tags = await _context.Tags
            .Include(t => t.TodoItems)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return tags.Select(MapToDto).ToList();
    }

    public async Task<TagDto?> GetTagByIdAsync(Guid tagId, Guid userId)
    {
        var tag = await _context.Tags
            .Include(t => t.TodoItems)
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

        return tag == null ? null : MapToDto(tag);
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request, Guid userId)
    {
        // Check if tag name already exists for this user
        if (await _context.Tags.AnyAsync(t => t.UserId == userId && t.Name == request.Name))
        {
            throw new InvalidOperationException("A tag with this name already exists");
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Color = request.Color,
            UserId = userId
        };

        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return MapToDto(tag);
    }

    public async Task<TagDto?> UpdateTagAsync(Guid tagId, UpdateTagRequest request, Guid userId)
    {
        var tag = await _context.Tags
            .Include(t => t.TodoItems)
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

        if (tag == null)
        {
            return null;
        }

        // Check if new name conflicts with existing tag
        if (tag.Name != request.Name && 
            await _context.Tags.AnyAsync(t => t.UserId == userId && t.Name == request.Name && t.Id != tagId))
        {
            throw new InvalidOperationException("A tag with this name already exists");
        }

        tag.Name = request.Name;
        tag.Color = request.Color;

        await _context.SaveChangesAsync();

        return MapToDto(tag);
    }

    public async Task<bool> DeleteTagAsync(Guid tagId, Guid userId)
    {
        var tag = await _context.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);

        if (tag == null)
        {
            return false;
        }

        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();

        return true;
    }

    private static TagDto MapToDto(Tag tag)
    {
        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            TodoCount = tag.TodoItems?.Count ?? 0
        };
    }
}

