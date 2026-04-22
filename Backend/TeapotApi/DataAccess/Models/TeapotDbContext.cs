using Microsoft.EntityFrameworkCore;

namespace DataAccess.Models;

public partial class TeapotDbContext : DbContext
{
    public TeapotDbContext()
    {
    }

    public TeapotDbContext(DbContextOptions<TeapotDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Invitation> Invitations { get; set; }

    public virtual DbSet<Membership> Memberships { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<TaskBlock> TaskBlocks { get; set; }

    public virtual DbSet<TaskDependency> TaskDependencies { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserTask> UserTasks { get; set; }

    public virtual DbSet<WorkProfile> WorkProfiles { get; set; }

    public virtual DbSet<WorkProfileTimeInterval> WorkProfileTimeIntervals { get; set; }

    public virtual DbSet<WorkDayProfile> WorkDayProfiles { get; set; }

    public virtual DbSet<WorkBlock> WorkBlocks { get; set; }

    public virtual DbSet<WorkBreak> WorkBreaks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(o => o.MapEnum<EInvitationStatus>("invitation_status")
                .MapEnum<ERole>("role")
                .MapEnum<ETaskPriority>("task_priority")
                .MapEnum<ETaskIntensity>("task_intensity"));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("invitation_status", ["open", "closed", "accepted", "expired"])
            .HasPostgresEnum("role", ["user", "organizer"])
            .HasPostgresEnum("task_intensity", ["light", "normal", "intensive"])
            .HasPostgresEnum("task_priority", ["low", "medium", "high"]);

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invitations_pkey");

            entity.ToTable("invitations");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EditedAt).HasColumnName("edited_at");
            entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Invitations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invitations_created_by_fkey");

            entity.HasOne(d => d.Organization).WithMany(p => p.Invitations)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("invitations_organization_id_fkey");
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("memberships_pkey");

            entity.ToTable("memberships");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Role).HasColumnName("role");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EditedAt).HasColumnName("edited_at");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Organization).WithMany(p => p.Memberships)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("memberships_organization_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Memberships)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("memberships_user_id_fkey");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organizations_pkey");

            entity.ToTable("organizations");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(100)
                .HasColumnName("description");
            entity.Property(e => e.EditedAt).HasColumnName("edited_at");
            entity.Property(e => e.MaxUsers).HasColumnName("max_users");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<TaskBlock>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("task_blocks");

            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsFixed).HasColumnName("is_fixed");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.TaskId).HasColumnName("task_id");

            entity.HasOne(d => d.Task).WithMany()
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("task_timeslots_task_id_fkey");
        });

        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("task_dependencies");

            entity.Property(e => e.DependsOnTaskId).HasColumnName("depends_on_task_id");
            entity.Property(e => e.TaskId).HasColumnName("task_id");

            entity.HasOne(d => d.DependsOnTask).WithMany()
                .HasForeignKey(d => d.DependsOnTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("task_dependencies_depends_on_task_id_fkey");

            entity.HasOne(d => d.Task).WithMany()
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("task_dependencies_task_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EditedAt).HasColumnName("edited_at");
            entity.Property(e => e.Email)
                .HasMaxLength(40)
                .HasColumnName("email");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");
        });

        modelBuilder.Entity<UserTask>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tasks_pkey");

            entity.ToTable("user_tasks");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Deadline).HasColumnName("deadline");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.Intensity).HasColumnName("intensity");
            entity.Property(e => e.EarlyFinish).HasColumnName("early_finish");
            entity.Property(e => e.EarlyStart).HasColumnName("early_start");
            entity.Property(e => e.EditedAt).HasColumnName("edited_at");
            entity.Property(e => e.IsFixed).HasColumnName("is_fixed");
            entity.Property(e => e.LateFinish).HasColumnName("late_finish");
            entity.Property(e => e.LateStart).HasColumnName("late_start");
            entity.Property(e => e.Name)
                .HasMaxLength(30)
                .HasColumnName("name");
            entity.Property(e => e.TimeEstimate).HasColumnName("time_estimate");
            entity.Property(e => e.WorkProfileId).HasColumnName("work_profile_id");

            entity.HasOne(d => d.WorkProfile).WithMany(p => p.UserTasks)
                .HasForeignKey(d => d.WorkProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tasks_workprofile_id_fkey");
        });

        modelBuilder.Entity<WorkProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("work_profiles_pkey");

            entity.ToTable("work_profiles");

            entity.HasIndex(e => e.MembershipId, "work_profiles_membership_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.EditedAt).HasColumnName("edited_at");
            entity.Property(e => e.MaxDailyLoad).HasColumnName("max_daily_load");
            entity.Property(e => e.MembershipId).HasColumnName("membership_id");
            entity.Property(e => e.PlannerViewStart)
                .HasMaxLength(5)
                .HasColumnName("planner_view_start")
                .HasDefaultValue("06:00");
            entity.Property(e => e.PlannerViewEnd)
                .HasMaxLength(5)
                .HasColumnName("planner_view_end")
                .HasDefaultValue("22:00");

            entity.HasOne(d => d.Membership).WithOne(p => p.WorkProfile)
                .HasForeignKey<WorkProfile>(d => d.MembershipId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("work_profiles_membership_id_fkey");
        });

        modelBuilder.Entity<WorkDayProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("work_day_profiles_pkey");

            entity.ToTable("work_day_profiles");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.WorkProfileId).HasColumnName("work_profile_id");
            entity.Property(e => e.Day)
                .HasMaxLength(3)
                .HasColumnName("day");

            entity.HasOne(d => d.WorkProfile).WithMany(p => p.Days)
                .HasForeignKey(d => d.WorkProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("work_day_profiles_work_profile_id_fkey");
        });

        modelBuilder.Entity<WorkBlock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("work_blocks_pkey");

            entity.ToTable("work_blocks");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.WorkDayProfileId).HasColumnName("work_day_profile_id");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CompanyName).HasColumnName("company_name");
            entity.Property(e => e.StartTime).HasMaxLength(5).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasMaxLength(5).HasColumnName("end_time");

            entity.HasOne(d => d.WorkDayProfile).WithMany(p => p.Blocks)
                .HasForeignKey(d => d.WorkDayProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("work_blocks_work_day_profile_id_fkey");
        });

        modelBuilder.Entity<WorkBreak>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("work_breaks_pkey");

            entity.ToTable("work_breaks");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.WorkDayProfileId).HasColumnName("work_day_profile_id");
            entity.Property(e => e.StartTime).HasMaxLength(5).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasMaxLength(5).HasColumnName("end_time");

            entity.HasOne(d => d.WorkDayProfile).WithMany(p => p.Breaks)
                .HasForeignKey(d => d.WorkDayProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("work_breaks_work_day_profile_id_fkey");
        });

        modelBuilder.Entity<WorkProfileTimeInterval>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("work_profile_time_intervals");

            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.WorkProfileId).HasColumnName("work_profile_id");

            entity.HasOne(d => d.WorkProfile).WithMany()
                .HasForeignKey(d => d.WorkProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("time_intervals_work_profile_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}