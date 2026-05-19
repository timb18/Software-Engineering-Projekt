namespace DataAccess.Models;

/// <summary>
/// Represents a user's membership in an organization with an assigned role.
/// Links users to organizations and defines their permissions within that organization.
/// </summary>
/// <remarks>
/// Each membership has:
/// - Role (User or Organizer): Determines what actions the member can perform
/// - Optional WorkProfile: The user's work schedule and task planning profile for this organization
/// 
/// A user can have multiple memberships (one per organization).
/// Organizers can manage invitations, add/remove members, and configure organization settings.
/// Regular users can only view and edit their own tasks and schedule.
/// </remarks>
public class Membership
{
    /// <summary>Unique identifier for this membership record</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key: ID of the user</summary>
    public Guid UserId { get; set; }

    /// <summary>Foreign key: ID of the organization</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The user's role within this organization (User or Organizer)</summary>
    public ERole Role { get; set; }

    /// <summary>Timestamp when the user joined the organization</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the membership was last modified (e.g., role change)</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Navigation property: the organization this user belongs to</summary>
    public virtual Organization Organization { get; set; } = null!;

    /// <summary>Navigation property: the user for this membership</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>Navigation property: optional work profile with schedule and task planning for this membership (null if not created)</summary>
    public virtual WorkProfile? WorkProfile { get; set; }
}