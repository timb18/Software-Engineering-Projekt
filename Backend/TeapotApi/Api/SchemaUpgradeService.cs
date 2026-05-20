using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Api;

/// <summary>
/// Handles schema migrations and data cleanup for the Teapot database.
/// Executed during application startup to ensure the database schema is up-to-date
/// and to remove orphaned personal workspace data that should no longer exist.
/// </summary>
public static class SchemaUpgradeService
{
    /// <summary>
    /// Applies all pending schema upgrades and data cleanup operations to the database.
    /// This method is idempotent - it can be safely called multiple times without data loss.
    /// Uses temporary tables to identify and safely delete orphaned personal workspace records.
    /// </summary>
    /// <param name="services">The service provider for resolving the database context</param>
    /// <remarks>
    /// Operations performed:
    /// 1. Schema updates: Modify user table columns and add missing fields (auth_provider_subject, display_name, etc.)
    /// 2. Index creation: Add unique index on auth_provider_subject for efficient lookups
    /// 3. Data cleanup: Identify and delete orphaned records from auto-created personal workspaces
    /// Skips execution if the database is not relational (e.g., in-memory database for testing)
    /// </remarks>
    public static async Task ApplyAsync(IServiceProvider services)
    {
        // Create a service scope to resolve the database context
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeapotDbContext>();

        // Skip for non-relational databases (in-memory, SQLite, etc.)
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        // Execute all schema upgrades and data cleanup in a single transaction
        // SQL operations are organized into two main sections: schema updates and data cleanup
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            -- ===== SCHEMA UPDATES: Ensure all required user columns exist =====
            ALTER TABLE users ALTER COLUMN email TYPE character varying(255);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS auth_provider_subject character varying(255);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS display_name character varying(120);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS profile_image_url character varying(500);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS timezone character varying(100);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS break_color text;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS blocker_color text;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS org_colors text;

            -- Work breaks are part of the scheduler contract. Older databases may
            -- have work profiles and blocks but no persisted break table yet.
            CREATE TABLE IF NOT EXISTS work_breaks (
                id uuid DEFAULT gen_random_uuid() NOT NULL,
                work_day_profile_id uuid NOT NULL,
                start_time character varying(5) NOT NULL DEFAULT '12:00',
                end_time character varying(5) NOT NULL DEFAULT '12:30'
            );

            ALTER TABLE work_breaks ADD COLUMN IF NOT EXISTS start_time character varying(5) NOT NULL DEFAULT '12:00';
            ALTER TABLE work_breaks ADD COLUMN IF NOT EXISTS end_time character varying(5) NOT NULL DEFAULT '12:30';
            ALTER TABLE work_breaks ADD COLUMN IF NOT EXISTS work_day_profile_id uuid;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'work_breaks_pkey'
                ) THEN
                    ALTER TABLE work_breaks ADD CONSTRAINT work_breaks_pkey PRIMARY KEY (id);
                END IF;

                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'work_breaks_work_day_profile_id_fkey'
                ) THEN
                    ALTER TABLE work_breaks
                    ADD CONSTRAINT work_breaks_work_day_profile_id_fkey
                    FOREIGN KEY (work_day_profile_id)
                    REFERENCES work_day_profiles(id)
                    ON DELETE CASCADE;
                END IF;
            END $$;
            
            -- Populate new user columns with sensible defaults if empty
            UPDATE users
            SET display_name = COALESCE(NULLIF(display_name, ''), username),
                timezone = COALESCE(NULLIF(timezone, ''), 'Europe/Berlin')
            WHERE display_name IS NULL OR timezone IS NULL OR timezone = '';
            
            -- Create unique index for Auth0 subject identifier (used for OAuth login)
            CREATE UNIQUE INDEX IF NOT EXISTS users_auth_provider_subject_key
            ON users(auth_provider_subject)
            WHERE auth_provider_subject IS NOT NULL;

            -- ===== DATA CLEANUP: Remove orphaned personal workspace records =====
            -- Personal workspaces are auto-created single-user workspaces that should be cleaned up if no longer needed
            -- This section uses temporary tables to safely identify cascading dependencies before deletion
            
            -- Drop any leftover temporary tables from previous runs
            DROP TABLE IF EXISTS teapot_personal_orgs_to_delete;
            DROP TABLE IF EXISTS teapot_personal_memberships_to_delete;
            DROP TABLE IF EXISTS teapot_personal_work_profiles_to_delete;
            DROP TABLE IF EXISTS teapot_personal_work_days_to_delete;
            DROP TABLE IF EXISTS teapot_personal_tasks_to_delete;

            -- Identify all personal organizations (max_users=1 with description 'Auto-created personal workspace')
            CREATE TEMP TABLE teapot_personal_orgs_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM organizations
            WHERE max_users = 1
              AND description = 'Auto-created personal workspace';

            -- Identify all memberships belonging to personal organizations
            CREATE TEMP TABLE teapot_personal_memberships_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM memberships
            WHERE organization_id IN (SELECT id FROM teapot_personal_orgs_to_delete);

            -- Identify all work profiles for memberships in personal organizations
            CREATE TEMP TABLE teapot_personal_work_profiles_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM work_profiles
            WHERE membership_id IN (SELECT id FROM teapot_personal_memberships_to_delete);

            -- Identify all work day profiles (schedules) for the personal work profiles
            CREATE TEMP TABLE teapot_personal_work_days_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM work_day_profiles
            WHERE work_profile_id IN (SELECT id FROM teapot_personal_work_profiles_to_delete);

            -- Identify all tasks assigned to personal work profiles
            CREATE TEMP TABLE teapot_personal_tasks_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM user_tasks
            WHERE work_profile_id IN (SELECT id FROM teapot_personal_work_profiles_to_delete);

            -- Delete in correct order to maintain referential integrity
            DELETE FROM task_dependencies
            WHERE task_id IN (SELECT id FROM teapot_personal_tasks_to_delete)
               OR depends_on_task_id IN (SELECT id FROM teapot_personal_tasks_to_delete);

            DELETE FROM task_blocks
            WHERE task_id IN (SELECT id FROM teapot_personal_tasks_to_delete);

            DELETE FROM user_tasks
            WHERE id IN (SELECT id FROM teapot_personal_tasks_to_delete);

            DELETE FROM work_profile_time_intervals
            WHERE work_profile_id IN (SELECT id FROM teapot_personal_work_profiles_to_delete);

            DELETE FROM work_blocks
            WHERE work_day_profile_id IN (SELECT id FROM teapot_personal_work_days_to_delete);

            DELETE FROM work_breaks
            WHERE work_day_profile_id IN (SELECT id FROM teapot_personal_work_days_to_delete);

            DELETE FROM work_day_profiles
            WHERE id IN (SELECT id FROM teapot_personal_work_days_to_delete);

            DELETE FROM work_profiles
            WHERE id IN (SELECT id FROM teapot_personal_work_profiles_to_delete);

            DELETE FROM invitations
            WHERE organization_id IN (SELECT id FROM teapot_personal_orgs_to_delete);

            DELETE FROM memberships
            WHERE id IN (SELECT id FROM teapot_personal_memberships_to_delete);

            DELETE FROM organizations
            WHERE id IN (SELECT id FROM teapot_personal_orgs_to_delete);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS public.recurring_blockers (
                id uuid DEFAULT gen_random_uuid() NOT NULL,
                work_profile_id uuid NOT NULL,
                name character varying(100) NOT NULL,
                days_of_week character varying(31) NOT NULL,
                start_time character varying(5) NOT NULL,
                end_time character varying(5) NOT NULL,
                valid_from date,
                valid_until date,
                created_at timestamp with time zone DEFAULT now() NOT NULL,
                edited_at timestamp with time zone,
                CONSTRAINT recurring_blockers_pkey PRIMARY KEY (id),
                CONSTRAINT recurring_blockers_work_profile_id_fkey
                    FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id) ON DELETE CASCADE
            );
            """);
    }
}
