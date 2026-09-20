using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/todos")]
[Authorize(Policy = "RequireFreelancer")]
public class TodosController : ControllerBase
{
    private readonly WebOfWorkDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TodosController(WebOfWorkDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTodos()
    {
        var todos = await _db.Set<FreelancerTodo>().Where(t => t.UserId == _currentUser.Id!.Value).ToListAsync();
        return Ok(todos);
    }

    [HttpPost]
    public async Task<IActionResult> AddTodo([FromBody] string task)
    {
        var todo = new FreelancerTodo { UserId = _currentUser.Id!.Value, Todo = task, IsCompleted = false };
        _db.Set<FreelancerTodo>().Add(todo);
        await _db.SaveChangesAsync();
        return Ok(todo);
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleTodo(Guid id)
    {
        var todo = await _db.Set<FreelancerTodo>().FirstOrDefaultAsync(t => t.Id == id && t.UserId == _currentUser.Id!.Value);
        if (todo == null) return NotFound();
        todo.IsCompleted = !todo.IsCompleted;
        await _db.SaveChangesAsync();
        return Ok(todo);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodo(Guid id)
    {
        var todo = await _db.Set<FreelancerTodo>().FirstOrDefaultAsync(t => t.Id == id && t.UserId == _currentUser.Id!.Value);
        if (todo == null) return NotFound();
        _db.Set<FreelancerTodo>().Remove(todo);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
