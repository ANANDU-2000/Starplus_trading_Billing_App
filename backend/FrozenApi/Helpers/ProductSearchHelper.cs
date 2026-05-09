/*
 * Normalized product search for PostgreSQL (ILIKE) and shared trim rules.
 */
namespace FrozenApi.Helpers
{
    public static class ProductSearchHelper
    {
        public static string NormalizeQuery(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;
            return query.Trim();
        }

        /// <summary>Escape % and _ for use in ILIKE patterns (PostgreSQL).</summary>
        public static string EscapeIlikePattern(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }

        public static string ToContainsPattern(string normalizedTerm)
        {
            if (string.IsNullOrEmpty(normalizedTerm)) return "%";
            return "%" + EscapeIlikePattern(normalizedTerm) + "%";
        }
    }
}
