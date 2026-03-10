/*
 * Use when API hits missing-column errors (e.g. 42703) so clients see "Database schema outdated" instead of generic errors.
 */
using Npgsql;

namespace FrozenApi.Helpers
{
    public static class SchemaOutdatedHelper
    {
        public const string SchemaOutdatedMessage =
            "Database schema outdated. Please run ApplyMissingSchema.sql on your database or redeploy so migrations can run.";

        public static bool IsSchemaOutdated(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "42703") return true;
                if (e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                    e.Message.Contains("MissingColumn", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
