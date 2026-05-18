using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

public record PlanningResultResponse(
    bool Success,
    string? ErrorMessage,
    int BacktrackingCount,
    IReadOnlyList<TaskBlock> PlannedBlocks);

public record TaskBlockResponse(
    Guid TaskId,
    string TaskName,
    string? TaskStatus,
    DateTime StartDate,
    DateTime EndDate,
    bool IsFixed);

[Route("api/planning/{workProfileId:guid}")]
[ApiController]
public class PlanningController(IUserTaskPlanner taskPlanner, ITaskBlockRepository taskBlockRepository) : ControllerBase
{
    /// <summary>
    /// Generates a work plan for all open tasks in the given work profile.
    /// Runs dependency analysis, critical path computation, and recursive scheduling.
    /// </summary>
    [HttpPost("schedule")]
    [ProducesResponseType(typeof(PlanningResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PlanningResultResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Schedule(Guid workProfileId, CancellationToken cancellationToken)
    {
        var result = await taskPlanner.ScheduleAsync(workProfileId, cancellationToken);

        var response = new PlanningResultResponse(
            result.Success,
            result.ErrorMessage,
            result.BacktrackingCount,
            result.PlannedBlocks);

        return result.Success
            ? Ok(response)
            : UnprocessableEntity(response);
    }

    /// <summary>Returns all task blocks for the given work profile for calendar rendering.</summary>
    [HttpGet("blocks")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskBlockResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlocks(Guid workProfileId, CancellationToken cancellationToken)
    {
        var blocks = await taskBlockRepository.GetByWorkProfileAsync(workProfileId, cancellationToken);
        var response = blocks.Select(b => new TaskBlockResponse(
            b.TaskId,
            b.Task.Name,
            b.Task.Status,
            b.StartDate,
            b.EndDate,
            b.IsFixed));
        return Ok(response);
    }
}

public record WorkProfileSaveRequest(
    string? MaxDailyLoad,
    string? PlannerViewStart,
    string? PlannerViewEnd,
    List<WorkDayProfileRequest>? Days);

public record WorkDayProfileRequest(
    string Day,
    List<WorkBlockRequest>? Blocks,
    List<WorkBreakRequest>? Breaks);

public record WorkBlockRequest(
    Guid? Id,
    string? CompanyId,
    string? CompanyName,
    string? StartTime,
    string? EndTime);

public record WorkBreakRequest(
    Guid? Id,
    string? StartTime,
    string? EndTime);

[Route("api/[controller]/{userId:guid}")]
[ApiController]
public class WorkProfileController(IWorkProfileService workProfileService) : ControllerBase
{
    /// <summary>Returns the work profile for a user. Returns 204 No Content if none exists yet.</summary>
    [HttpGet("")]
    [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await workProfileService.GetAsync(userId, cancellationToken);
        if (profile is null)
            return NoContent();

        return Ok(profile);
    }

    /// <summary>Creates or replaces the work profile for a user.</summary>
    [HttpPut("")]
    [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(Guid userId, [FromBody] WorkProfileSaveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = MapRequestToWorkProfile(request);
            var saved = await workProfileService.SaveAsync(userId, profile, cancellationToken);
            return Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Deletes the work profile and dependent planning data for a user.</summary>
    [HttpDelete("")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await workProfileService.DeleteAsync(userId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("/api/WorkProfile/by-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteByEmail([FromQuery] string email, CancellationToken cancellationToken)
    {
        try
        {
            await workProfileService.DeleteByEmailAsync(email, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static WorkProfile MapRequestToWorkProfile(WorkProfileSaveRequest request)
    {
        var maxDailyLoad = TimeSpan.Zero;
        if (!string.IsNullOrWhiteSpace(request.MaxDailyLoad) &&
            !TimeSpan.TryParse(request.MaxDailyLoad, out maxDailyLoad))
        {
            throw new ArgumentException($"Invalid MaxDailyLoad format: '{request.MaxDailyLoad}'. Expected HH:mm:ss.");
        }

        return new WorkProfile
        {
            MaxDailyLoad = maxDailyLoad,
            PlannerViewStart = string.IsNullOrWhiteSpace(request.PlannerViewStart) ? "06:00" : request.PlannerViewStart,
            PlannerViewEnd = string.IsNullOrWhiteSpace(request.PlannerViewEnd) ? "22:00" : request.PlannerViewEnd,
            Days = (request.Days ?? []).Select(day => new WorkDayProfile
            {
                Day = day.Day,
                Blocks = (day.Blocks ?? []).Select(block => new WorkBlock
                {
                    Id = block.Id ?? Guid.Empty,
                    CompanyId = block.CompanyId ?? string.Empty,
                    CompanyName = block.CompanyName ?? string.Empty,
                    StartTime = string.IsNullOrWhiteSpace(block.StartTime) ? "09:00" : block.StartTime,
                    EndTime = string.IsNullOrWhiteSpace(block.EndTime) ? "17:00" : block.EndTime,
                }).ToList(),
                Breaks = (day.Breaks ?? []).Select(workBreak => new WorkBreak
                {
                    Id = workBreak.Id ?? Guid.Empty,
                    StartTime = string.IsNullOrWhiteSpace(workBreak.StartTime) ? "12:00" : workBreak.StartTime,
                    EndTime = string.IsNullOrWhiteSpace(workBreak.EndTime) ? "12:30" : workBreak.EndTime,
                }).ToList(),
            }).ToList(),
        };
    }
}