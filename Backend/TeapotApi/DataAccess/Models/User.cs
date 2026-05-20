namespace DataAccess.Models;

/// <summary>
///     Represents a user account in the Teapot system.
///     Users can join multiple organizations and have distinct roles within each.
/// </summary>
/// <remarks>
///     A user has either Auth0-authenticated identity (via AuthProviderSubject) or a traditional username/email
///     combination.
///     The display name is shown in the UI while email is used for invitations and account recovery.
///     Timezone determines how dates and scheduled times are displayed to the user.
///     BreakColor and OrgColors store user UI preferences for work breaks and organization color schemes.
/// </remarks>
public class User
{
    /// <summary>Unique identifier for the user</summary>
    public Guid Id { get; set; }

    /// <summary>Auth0 unique subject identifier for OAuth authentication (null if using traditional auth)</summary>
    public string? AuthProviderSubject { get; set; }

    /// <summary>Username for traditional login (may be null for OAuth-only users)</summary>
    public string? Username { get; set; }

    /// <summary>Display name shown in the user interface (typically first and last name)</summary>
    public string? DisplayName { get; set; }

    /// <summary>Email address - must be unique and is used for invitations and notifications</summary>
    public string Email { get; set; } = null!;

    /// <summary>URL to user's profile image from Auth0 or uploaded source</summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>User's timezone for displaying dates and times (e.g., "Europe/Berlin", "America/New_York")</summary>
    public string? Timezone { get; set; }

    /// <summary>Color preference for work breaks (hex color code like "#FF5733")</summary>
    public string? BreakColor { get; set; }

    public string? BlockerColor { get; set; }

    /// <summary>Serialized organization color preferences (JSON mapping organization IDs to colors)</summary>
    public string? OrgColors { get; set; }

    /// <summary>Timestamp when the user account was created</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the user profile was last edited (null if never edited)</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Navigation property: all invitations sent to this user's email</summary>
    public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();

    /// <summary>Navigation property: all organization memberships for this user</summary>
    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}