using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Categories;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public class CategoryService : ICategoryService
{
    private readonly WebOfWorkDbContext _context;

    public CategoryService(WebOfWorkDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CategoryResponse>> GetCategoriesAsync()
    {
        return await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var exists = await _context.Categories.AnyAsync(c => c.Name == request.Name);
        if (exists)
            throw new InvalidOperationException("Category name must be unique.");

        var category = new Category { Name = request.Name };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return new CategoryResponse { Id = category.Id, Name = category.Name };
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(int id, CreateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) throw new InvalidOperationException("Category not found.");

        var exists = await _context.Categories.AnyAsync(c => c.Name == request.Name && c.Id != id);
        if (exists) throw new InvalidOperationException("Category name must be unique.");

        category.Name = request.Name;
        await _context.SaveChangesAsync();

        return new CategoryResponse { Id = category.Id, Name = category.Name };
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Skills)
            .Include(c => c.Profiles)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null) throw new InvalidOperationException("Category not found.");

        if (category.Skills.Any() || category.Profiles.Any())
            throw new InvalidOperationException("Cannot delete a category that has associated skills or profiles.");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<SkillResponse>> GetSkillsAsync(int? categoryId = null)
    {
        var query = _context.Skills.AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(s => s.CategoryId == categoryId.Value);

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SkillResponse 
            { 
                Id = s.Id, 
                Name = s.Name, 
                CategoryId = s.CategoryId ?? 0, 
                IsCustom = s.IsCustom 
            })
            .ToListAsync();
    }

    public async Task<SkillResponse> CreateSkillAsync(CreateSkillRequest request)
    {
        var exists = await _context.Skills.AnyAsync(s => s.Name == request.Name && s.CategoryId == request.CategoryId);
        if (exists) throw new InvalidOperationException("Skill name must be unique within a category.");

        var skill = new Skill
        {
            Name = request.Name,
            CategoryId = request.CategoryId,
            IsCustom = request.IsCustom
        };

        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();

        return new SkillResponse { Id = skill.Id, Name = skill.Name, CategoryId = skill.CategoryId ?? 0, IsCustom = skill.IsCustom };
    }

    public async Task<SkillResponse> UpdateSkillAsync(int id, CreateSkillRequest request)
    {
        var skill = await _context.Skills.FindAsync(id);
        if (skill == null) throw new InvalidOperationException("Skill not found.");

        var exists = await _context.Skills.AnyAsync(s => s.Name == request.Name && s.CategoryId == request.CategoryId && s.Id != id);
        if (exists) throw new InvalidOperationException("Skill name must be unique within a category.");

        skill.Name = request.Name;
        skill.CategoryId = request.CategoryId;
        skill.IsCustom = request.IsCustom;

        await _context.SaveChangesAsync();

        return new SkillResponse { Id = skill.Id, Name = skill.Name, CategoryId = skill.CategoryId ?? 0, IsCustom = skill.IsCustom };
    }

    public async Task DeleteSkillAsync(int id)
    {
        var skill = await _context.Skills.FindAsync(id);
        if (skill == null) throw new InvalidOperationException("Skill not found.");

        var inUse = await _context.FreelancerSkills.AnyAsync(fs => fs.SkillId == id);
        if (inUse)
            throw new InvalidOperationException("Cannot delete a skill that is used by freelancers.");

        _context.Skills.Remove(skill);
        await _context.SaveChangesAsync();
    }
}
