namespace DataAccess.Models;

/// <summary>
///     Represents the lifecycle status of an organization invitation.
/// </summary>
/// <remarks>
///     Invitations progress through these states:
///     - Open: Newly created, awaiting user action
///     - Accepted: User has accepted the invitation and joined the organization
///     - Closed: Invitation was manually revoked by the organizer
///     - Expired: Invitation passed its expiry date without being accepted
/// </remarks>
public enum EInvitationStatus
{
    /// <summary>Invitation is pending, waiting for user action (send acceptance or rejection)</summary>
    Open,

    /// <summary>Invitation was revoked by the organizer before user acceptance</summary>
    Closed,

    /// <summary>User accepted the invitation and joined the organization</summary>
    Accepted,

    /// <summary>Invitation automatically expired after reaching the expiry date</summary>
    Expired
}