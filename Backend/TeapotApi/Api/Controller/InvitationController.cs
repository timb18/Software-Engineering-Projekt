using Microsoft.AspNetCore.Mvc;
using Services;

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

        [HttpPost("create")]
        public IActionResult Create(Guid organizationId, string email)
        {
            var createdByUserId = Guid.NewGuid(); // hier später echten User aus Auth holen

            var invitation = _invitationService.CreateInvitation(organizationId, createdByUserId);

            // hier später Mail an email senden
            // z.B. invitation.Id in Link einbauen

            return Ok(new
            {
                invitationId = invitation.Id,
                expiresAt = invitation.ExpiryDate
            });
        }

        [HttpPost("accept/{invitationId}")]
        public IActionResult Accept(Guid invitationId, Guid userId)
        {
            _invitationService.AcceptInvitation(invitationId, userId);
            return Ok();
        }
    }
}