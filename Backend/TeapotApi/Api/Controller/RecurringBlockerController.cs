using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/recurring-blocker/{workProfileId:guid}")]
[ApiController]
[Authorize(AuthenticationSchemes = "Auth0")]
public class RecurringBlockerController(IRecurringBlockerRepository repository) : ControllerBase
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    /// <summary>
    /// Retrieves a collection of recurring blockers for the specified work profile.
    /// </summary>
    /// <param name="workProfileId">The unique identifier of the work profile to filter the blockers by.</param>
    /// <param name="cancellationToken">A cancellation token that allows the operation to be cancelled if necessary.</param>
    /// <returns>An IActionResult containing the list of recurring blockers associated with the work profile.</returns>
    [HttpGet("")]
    [ProducesResponseType(typeof(IEnumerable<RecurringBlocker>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid workProfileId, CancellationToken cancellationToken)
    {
        var blockers = await repository.GetByWorkProfileAsync(workProfileId, cancellationToken);
        return Ok(blockers);
    }

    /// <summary>Creates a new recurring blocker.</summary>
    [HttpPost("")]
    [ProducesResponseType(typeof(RecurringBlocker), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid workProfileId, [FromBody] RecurringBlocker blocker, CancellationToken cancellationToken)
    {
        var validationError = Validate(blocker);
        if (validationError is not null)
            return BadRequest(validationError);

        blocker.Id = Guid.NewGuid();
        blocker.WorkProfileId = workProfileId;
        blocker.CreatedAt = DateTime.UtcNow;
        blocker.EditedAt = null;

        await repository.AddAsync(blocker, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { workProfileId }, blocker);
    }

    /// <summary>Updates an existing recurring blocker.</summary>
    [HttpPut("{blockerId:guid}")]
    [ProducesResponseType(typeof(RecurringBlocker), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid workProfileId, Guid blockerId, [FromBody] RecurringBlocker blocker, CancellationToken cancellationToken)
    {
        var validationError = Validate(blocker);
        if (validationError is not null)
            return BadRequest(validationError);

        var existing = await repository.GetByIdAsync(workProfileId, blockerId, cancellationToken);
        if (existing is null)
            return NotFound();

        existing.Name = blocker.Name;
        existing.DaysOfWeek = blocker.DaysOfWeek;
        existing.StartTime = blocker.StartTime;
        existing.EndTime = blocker.EndTime;
        existing.ValidFrom = blocker.ValidFrom;
        existing.ValidUntil = blocker.ValidUntil;
        existing.EditedAt = DateTime.UtcNow;

        await repository.UpdateAsync(existing, cancellationToken);
        return Ok(existing);
    }

    /// <summary>Deletes a recurring blocker.</summary>
    [HttpDelete("{blockerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid workProfileId, Guid blockerId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(workProfileId, blockerId, cancellationToken);
        if (existing is null)
            return NotFound();

        await repository.DeleteAsync(existing, cancellationToken);
        return NoContent();
    }

    private static string? Validate(RecurringBlocker blocker)
    {
        if (string.IsNullOrWhiteSpace(blocker.Name))
            return "Name is required.";
        if (string.IsNullOrWhiteSpace(blocker.DaysOfWeek))
            return "DaysOfWeek is required.";
        if (string.IsNullOrWhiteSpace(blocker.StartTime) || string.IsNullOrWhiteSpace(blocker.EndTime))
            return "StartTime and EndTime are required.";

        var days = blocker.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (days.Length == 0 || days.Any(d => !ValidDays.Contains(d, StringComparer.OrdinalIgnoreCase)))
            return $"DaysOfWeek must be comma-separated values from: {string.Join(", ", ValidDays)}.";

        if (blocker.ValidFrom.HasValue && blocker.ValidUntil.HasValue && blocker.ValidFrom > blocker.ValidUntil)
            return "ValidFrom must not be after ValidUntil.";

        return null;
    }
}
