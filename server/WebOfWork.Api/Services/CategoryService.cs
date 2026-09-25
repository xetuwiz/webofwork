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
    public CategoryService(WebOfWorkDbContext context) => _context = context;

    public async Task<IEnumerable<CategoryResponse>> GetCategoriesAsync(bool noPagination = false) => await _context.Categories.OrderBy(c => c.Id).Select(c => new CategoryResponse { Id = c.Id, Name = c.Name }).ToListAsync();
    public async Task<CategoryResponse> GetCategoryAsync(int id) => await _context.Categories.AsNoTracking().Where(c => c.Id == id).Select(c => new CategoryResponse { Id = c.Id, Name = c.Name }).FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Category not found.");

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Category name cannot be empty");
        if (await _context.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower())) throw new InvalidOperationException("Category already exists");
        var category = new Category { Name = name }; _context.Categories.Add(category); await _context.SaveChangesAsync();
        return new CategoryResponse { Id = category.Id, Name = category.Name };
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(int id, CreateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id) ?? throw new KeyNotFoundException("Category not found.");
        var name = request.Name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Category name cannot be empty");
        if (await _context.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower())) throw new InvalidOperationException("Category already exists");
        category.Name = name; await _context.SaveChangesAsync(); return new CategoryResponse { Id = category.Id, Name = category.Name };
    }

    public async Task DeleteCategoryAsync(int id)
    {
        if (await _context.Skills.AnyAsync(s => s.CategoryId == id)) throw new InvalidOperationException("This category is in use and cannot be deleted.");
        var category = await _context.Categories.FindAsync(id) ?? throw new KeyNotFoundException("Category not found.");
        _context.Categories.Remove(category); await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<SkillResponse>> GetSkillsAsync(int? categoryId = null)
    {
        var query = _context.Skills.AsNoTracking().Include(s => s.Category).AsQueryable();
        if (categoryId.HasValue) query = query.Where(s => s.CategoryId == categoryId.Value);
        return await query.OrderBy(s => s.Id).Select(s => new SkillResponse { Id = s.Id, Name = s.Name, CategoryId = s.CategoryId ?? 0, Category = s.CategoryId ?? 0, CategoryName = s.Category == null ? null : s.Category.Name, IsCustom = s.IsCustom }).ToListAsync();
    }

    public async Task<SkillResponse> GetSkillAsync(int id) => await _context.Skills.AsNoTracking().Include(s => s.Category).Where(s => s.Id == id).Select(s => new SkillResponse { Id = s.Id, Name = s.Name, CategoryId = s.CategoryId ?? 0, Category = s.CategoryId ?? 0, CategoryName = s.Category == null ? null : s.Category.Name, IsCustom = s.IsCustom }).FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Skill not found.");

    public async Task<SkillResponse> CreateSkillAsync(CreateSkillRequest request)
    {
        var categoryId = request.Category == 0 ? request.CategoryId : request.Category;
        var name = request.Name.Trim(); if (name.Length == 0) throw new InvalidOperationException("Skill name cannot be empty");
        if (!await _context.Categories.AnyAsync(c => c.Id == categoryId)) throw new InvalidOperationException("Category not found.");
        if (await _context.Skills.AnyAsync(s => s.CategoryId == categoryId && s.Name.ToLower() == name.ToLower())) throw new InvalidOperationException("This skill already exists in the selected category.");
        var skill = new Skill { Name = name, CategoryId = categoryId, IsCustom = request.IsCustom }; _context.Skills.Add(skill); await _context.SaveChangesAsync();
        return await GetSkillAsync(skill.Id);
    }

    public async Task<SkillResponse> UpdateSkillAsync(int id, CreateSkillRequest request)
    {
        var skill = await _context.Skills.FindAsync(id) ?? throw new KeyNotFoundException("Skill not found.");
        var categoryId = request.Category == 0 ? request.CategoryId : request.Category; var name = request.Name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Skill name cannot be empty");
        if (!await _context.Categories.AnyAsync(c => c.Id == categoryId)) throw new InvalidOperationException("Category not found.");
        if (await _context.Skills.AnyAsync(s => s.Id != id && s.CategoryId == categoryId && s.Name.ToLower() == name.ToLower())) throw new InvalidOperationException("This skill already exists in the selected category.");
        skill.Name = name; skill.CategoryId = categoryId; skill.IsCustom = request.IsCustom; await _context.SaveChangesAsync(); return await GetSkillAsync(id);
    }

    public async Task DeleteSkillAsync(int id)
    {
        if (await _context.ProjectSkills.AnyAsync(x => x.SkillId == id) || await _context.FreelancerSkills.AnyAsync(x => x.SkillId == id)) throw new InvalidOperationException("This skill is currently being used in projects and cannot be deleted.");
        var skill = await _context.Skills.FindAsync(id) ?? throw new KeyNotFoundException("Skill not found."); _context.Skills.Remove(skill); await _context.SaveChangesAsync();
    }
}
