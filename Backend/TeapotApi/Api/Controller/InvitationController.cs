using Microsoft.AspNetCore.Mvc;
using Services;
using Api.Dto;

namespace Api
{
    [ApiController]
    [Route("api/invitations")]
    public class InvitationController : ControllerBase
    {
        private readonly InvitationService _invitationService;

        public InvitationController(InvitationService invitationService)
        {
            _invitationService = invitationService;
        }

        [HttpGet("remaining/{organizationId}")]
        public IActionResult GetRemaining(Guid organizationId)
        {
            var remaining = _invitationService.GetRemainingInvitations(organizationId);
            return Ok(remaining);
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateInvitationRequest request)
        {
            var createdByUserId = Guid.NewGuid(); // потом из auth

            var invitation = _invitationService.CreateInvitation(
                request.OrganizationId,
                createdByUserId,
                request.Email
            );

            return Ok(invitation);
        }

        [HttpPost("accept/{invitationId}")]
        public IActionResult Accept(Guid invitationId, Guid userId)
        {
            _invitationService.AcceptInvitation(invitationId, userId);
            return Ok();
        }
    }
}