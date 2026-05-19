using DataAccess.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/task/{workProfileId:guid}")]
[ApiController]
public class TaskController(IUserTaskService taskService) : ControllerBase
{
    /// <summary>Loads a single task for editing.</summary>
    /// <remarks>Returns the full UserTask object needed to populate the edit modal and preserve current values.</remarks>
    [HttpGet("{taskId:guid}")]
    [ProducesResponseType(typeof(UserTask), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask(
        Guid workProfileId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await taskService.GetTaskAsync(workProfileId, taskId, cancellationToken);
        if (task == null)
            return NotFound();
        return Ok(task);
    }
    /// <summary>Returns all tasks for the given work profile in a single list.</summary>
    [HttpGet("")]
    [ProducesResponseType(typeof(IEnumerable<UserTask>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks(Guid workProfileId, CancellationToken cancellationToken)
    {
        var tasks = await taskService.GetTasksAsync(workProfileId, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>Creates a new task for the given work profile after validating the task name.</summary>
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

    /// <summary>Updates an existing task and returns the persisted version.</summary>
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

    /// <summary>Deletes a task from the given work profile.</summary>
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
