namespace DataAccess.Models;

/// <summary>
///     Represents a team or organization workspace in the Teapot system.
///     Organizations contain members with different roles and manage collaborative work planning.
/// </summary>
/// <remarks>
///     An organization can be either:
///     - A shared team workspace (MaxUsers > 1): For teams collaborating on work planning
///     - A personal workspace (MaxUsers = 1): Auto-created personal workspace for individual users
///     Organizations have members (via Membership) and can invite new users (via Invitation).
/// </remarks>
public class Organization
{
    /// <summary>Unique identifier for the organization</summary>
    public Guid Id { get; set; }

    /// <summary>Organization name displayed in the UI</summary>
    public string Name { get; set; } = null!;

    /// <summary>Organization description and purpose</summary>
    public string Description { get; set; } = null!;

    /// <summary>Maximum number of users allowed in this organization</summary>
    /// <remarks>1 for personal workspaces, >1 for team organizations</remarks>
    public int MaxUsers { get; set; }

    /// <summary>Timestamp when the organization was created</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the organization was last modified (null if never edited)</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Navigation property: all pending and past invitations for this organization</summary>
    public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();

    /// <summary>Navigation property: all users who are members of this organization</summary>
    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}