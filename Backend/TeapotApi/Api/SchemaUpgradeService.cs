using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Api;

public static class SchemaUpgradeService
{
    public static async Task ApplyAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeapotDbContext>();

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE users ALTER COLUMN email TYPE character varying(255);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS auth_provider_subject character varying(255);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS display_name character varying(120);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS profile_image_url character varying(500);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS timezone character varying(100);
            ALTER TABLE users ADD COLUMN IF NOT EXISTS break_color text;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS blocker_color text;
            ALTER TABLE users ADD COLUMN IF NOT EXISTS org_colors text;
            UPDATE users
            SET display_name = COALESCE(NULLIF(display_name, ''), username),
                timezone = COALESCE(NULLIF(timezone, ''), 'Europe/Berlin')
            WHERE display_name IS NULL OR timezone IS NULL OR timezone = '';
            CREATE UNIQUE INDEX IF NOT EXISTS users_auth_provider_subject_key
            ON users(auth_provider_subject)
            WHERE auth_provider_subject IS NOT NULL;

            DROP TABLE IF EXISTS teapot_personal_orgs_to_delete;
            DROP TABLE IF EXISTS teapot_personal_memberships_to_delete;
            DROP TABLE IF EXISTS teapot_personal_work_profiles_to_delete;
            DROP TABLE IF EXISTS teapot_personal_work_days_to_delete;
            DROP TABLE IF EXISTS teapot_personal_tasks_to_delete;

            CREATE TEMP TABLE teapot_personal_orgs_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM organizations
            WHERE max_users = 1
              AND description = 'Auto-created personal workspace';

            CREATE TEMP TABLE teapot_personal_memberships_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM memberships
            WHERE organization_id IN (SELECT id FROM teapot_personal_orgs_to_delete);

            CREATE TEMP TABLE teapot_personal_work_profiles_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM work_profiles
            WHERE membership_id IN (SELECT id FROM teapot_personal_memberships_to_delete);

            CREATE TEMP TABLE teapot_personal_work_days_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM work_day_profiles
            WHERE work_profile_id IN (SELECT id FROM teapot_personal_work_profiles_to_delete);

            CREATE TEMP TABLE teapot_personal_tasks_to_delete
            ON COMMIT DROP AS
            SELECT id
            FROM user_tasks
            WHERE work_profile_id IN (SELECT id FROM teapot_personal_work_profiles_to_delete);

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
