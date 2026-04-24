using Microsoft.EntityFrameworkCore;
using Model;

namespace DataAccess;

public class TeapotDbContext : DbContext
{
    public TeapotDbContext(DbContextOptions<TeapotDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(255);
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("edited_at");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(100);
            entity.Property(x => x.MaxUsers).HasColumnName("max_user");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.EditedAt).HasColumnName("edited_at");
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.EditedAt).HasColumnName("edited_at");
            entity.Property(x => x.Role)
                .HasColumnName("role")
                .HasColumnType("role")
                .HasConversion(
                    value => value == ERole.Organizer ? "organizer" : "user",
                    value => string.Equals(value, "organizer", StringComparison.OrdinalIgnoreCase)
                        ? ERole.Organizer
                        : ERole.User);

            entity.HasOne(x => x.User)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.UserId);

            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100);
            entity.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.EditedAt).HasColumnName("edited_at");
            entity.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion(
                    value => value.ToString().ToLowerInvariant(),
                    value => ParseInvitationStatus(value));

            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Invitations)
                .HasForeignKey(x => x.OrganizationId);

            entity.HasOne(x => x.CreatedByNavigation)
                .WithMany(x => x.CreatedInvitations)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static EInvitationStatus ParseInvitationStatus(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "accepted" => EInvitationStatus.Accepted,
            "expired" => EInvitationStatus.Expired,
            "closed" => EInvitationStatus.Closed,
            _ => EInvitationStatus.Open
        };
    }
}
