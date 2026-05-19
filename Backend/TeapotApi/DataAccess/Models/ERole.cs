namespace DataAccess.Models;

/// <summary>
/// Defines the role of a user within an organization.
/// </summary>
/// <remarks>
/// Two roles are supported:
/// - User: Regular member of an organization with limited permissions
/// - Organizer: Administrator of an organization with full management permissions (create invitations, manage roles, etc.)
/// </remarks>
public enum ERole
{
    /// <summary>Regular organization member with limited permissions</summary>
    User,
    
    /// <summary>Organization administrator with full management permissions</summary>
    Organizer
}