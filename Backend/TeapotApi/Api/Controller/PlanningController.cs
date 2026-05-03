using Api.Dtos;
using DataAccess.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class PlanningController(IUserTaskPlanner userTaskPlanner) : ControllerBase
{
    /// <summary>Plans all open tasks for a work profile using constraints and backtracking algorithm.</summary>
    /// <param name="workProfileId">The ID of the work profile to plan tasks for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Planning result with assigned task blocks, conflicts, and warnings.</returns>
    [HttpPost("/api/[controller]/plan-tasks")]
    public async Task<ActionResult<PlanTasksResponseDto>> PlanTasks(Guid workProfileId, CancellationToken cancellationToken)
    {
        var (newBlocks, conflicts, warnings) = await userTaskPlanner.PlanTasks(workProfileId, cancellationToken);
        
        var response = new PlanTasksResponseDto
        {
            NewBlocks = newBlocks,
            Conflicts = conflicts,
            Warnings = warnings
        };
        
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
            WorkDayProfiles = (request.Days ?? []).Select(day => new WorkDayProfile
            {
                Day = day.Day,
                WorkBlocks = (day.Blocks ?? []).Select(block => new WorkBlock
                {
                    Id = block.Id ?? Guid.Empty,
                    CompanyId = block.CompanyId ?? string.Empty,
                    CompanyName = block.CompanyName ?? string.Empty,
                    StartTime = string.IsNullOrWhiteSpace(block.StartTime) ? "09:00" : block.StartTime,
                    EndTime = string.IsNullOrWhiteSpace(block.EndTime) ? "17:00" : block.EndTime,
                }).ToList(),
                WorkBreaks = (day.Breaks ?? []).Select(workBreak => new WorkBreak
                {
                    Id = workBreak.Id ?? Guid.Empty,
                    StartTime = string.IsNullOrWhiteSpace(workBreak.StartTime) ? "12:00" : workBreak.StartTime,
                    EndTime = string.IsNullOrWhiteSpace(workBreak.EndTime) ? "12:30" : workBreak.EndTime,
                }).ToList(),
            }).ToList(),
        };
    }
}
