using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebOfWork.Api.DTOs.Categories;

namespace WebOfWork.Api.Services;

public interface ICategoryService
{
    Task<IEnumerable<CategoryResponse>> GetCategoriesAsync(bool noPagination = false);
    Task<CategoryResponse> GetCategoryAsync(int id);
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryResponse> UpdateCategoryAsync(int id, CreateCategoryRequest request);
    Task DeleteCategoryAsync(int id);
    Task<IEnumerable<SkillResponse>> GetSkillsAsync(int? categoryId = null);
    Task<SkillResponse> GetSkillAsync(int id);
    Task<SkillResponse> CreateSkillAsync(CreateSkillRequest request);
    Task<SkillResponse> UpdateSkillAsync(int id, CreateSkillRequest request);
    Task DeleteSkillAsync(int id);
}
