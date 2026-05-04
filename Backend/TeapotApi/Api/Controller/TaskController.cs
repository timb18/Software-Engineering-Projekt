using DataAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/task/{workProfileId:guid}")]
[ApiController]
public class TaskController(IUserTaskService taskService) : ControllerBase
{
    /// <summary>Returns all tasks for the given work profile.</summary>
    [HttpGet("")]
    [ProducesResponseType(typeof(IEnumerable<UserTask>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks(Guid workProfileId, CancellationToken cancellationToken)
    {
        var tasks = await taskService.GetTasksAsync(workProfileId, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>Creates a new task for the given work profile.</summary>
    [HttpPost("")]
    [ProducesResponseType(typeof(UserTask), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTask(
        Guid workProfileId, [FromBody] UserTask task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
            return BadRequest("Task name is required.");

        var created = await taskService.CreateTaskAsync(workProfileId, task, cancellationToken);
        return CreatedAtAction(nameof(GetTasks), new { workProfileId }, created);
    }

    /// <summary>Updates an existing task.</summary>
    [HttpPut("{taskId:guid}")]
    [ProducesResponseType(typeof(UserTask), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(
        Guid workProfileId, Guid taskId, [FromBody] UserTask task, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await taskService.UpdateTaskAsync(workProfileId, taskId, task, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Deletes a task.</summary>
    [HttpDelete("{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(
        Guid workProfileId, Guid taskId, CancellationToken cancellationToken)
    {
        try
        {
            await taskService.DeleteTaskAsync(workProfileId, taskId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}