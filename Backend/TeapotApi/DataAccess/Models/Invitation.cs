namespace DataAccess.Models;

/// <summary>
/// Represents an invitation sent to a user email to join an organization.
/// Tracks invitation status, expiry, and basic recipient information.
/// </summary>
/// <remarks>
/// Invitations are created by organization organizers to invite new team members.
/// Each invitation:
/// - Has a unique link/token that can be emailed to the recipient
/// - Expires after a set time period (configurable)
/// - Can be manually closed (revoked) by the organizer
/// - Transitions to "Accepted" when the user joins via the invitation link
/// 
/// First/LastName are optional fields captured during the invitation creation for context.
/// </remarks>
public class Invitation
{
    /// <summary>Unique identifier for this invitation</summary>
    public Guid Id { get; set; }
    
    /// <summary>Foreign key: ID of the organization sending the invitation</summary>
    public Guid OrganizationId { get; set; }
    
    /// <summary>Foreign key: ID of the organizer who created this invitation</summary>
    public Guid CreatedBy { get; set; }
    
    /// <summary>Timestamp when the invitation was created</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Timestamp when the invitation was last modified (null if never modified)</summary>
    public DateTime? EditedAt { get; set; }
    
    /// <summary>Date/time when the invitation automatically expires (null if no expiry)</summary>
    public DateTime? ExpiryDate { get; set; }
    
    /// <summary>Current status of the invitation (Open, Closed, Accepted, or Expired)</summary>
    public EInvitationStatus Status { get; set; }

    /// <summary>Email address of the person being invited</summary>
    public string Email { get; set; } = null!;
    
    /// <summary>Optional first name of the invitee (for context/records)</summary>
    public string? FirstName { get; set; }
    
    /// <summary>Optional last name of the invitee (for context/records)</summary>
    public string? LastName { get; set; }

    /// <summary>Navigation property: the user who created this invitation</summary>
    public virtual User CreatedByNavigation { get; set; } = null!;
    
    /// <summary>Navigation property: the organization this invitation is for</summary>
    public virtual Organization Organization { get; set; } = null!;
}