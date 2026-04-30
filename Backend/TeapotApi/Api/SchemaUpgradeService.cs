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
            UPDATE users
            SET display_name = COALESCE(NULLIF(display_name, ''), username),
                timezone = COALESCE(NULLIF(timezone, ''), 'Europe/Berlin')
            WHERE display_name IS NULL OR timezone IS NULL OR timezone = '';
            CREATE UNIQUE INDEX IF NOT EXISTS users_auth_provider_subject_key
            ON users(auth_provider_subject)
            WHERE auth_provider_subject IS NOT NULL;
            """);
    }
}
