using Api.Dto;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class InvitationController : ControllerBase
{
    private readonly InvitationService _invitationService;

    public InvitationController(InvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpGet("remaining/{organizationId:guid}")]
    public async Task<IActionResult> GetRemaining(Guid organizationId)
    {
        var remaining = await _invitationService.GetRemainingInvitationsAsync(organizationId);
        return Ok(remaining);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] string email)
    {
        var invitations = await _invitationService.GetPendingInvitationsForEmailAsync(email);
        return Ok(invitations);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest request)
    {
        var invitation = await _invitationService.CreateInvitationAsync(request);
        return Ok(invitation);
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request)
    {
        await _invitationService.AcceptInvitationAsync(request.InvitationId, request.CurrentUserEmail);
        return Ok();
    }

    [HttpPost("decline")]
    public async Task<IActionResult> Decline([FromBody] DeclineInvitationRequest request)
    {
        await _invitationService.DeclineInvitationAsync(request.InvitationId, request.CurrentUserEmail);
        return Ok();
    }

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Withdraw(Guid invitationId, [FromBody] WithdrawInvitationRequest request)
    {
        await _invitationService.WithdrawInvitationAsync(invitationId, request.RequesterEmail);
        return Ok();
    }
}