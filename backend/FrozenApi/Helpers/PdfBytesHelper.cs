namespace FrozenApi.Helpers
{
    public static class PdfBytesHelper
    {
        public static byte[] EnsureValidPdfBytes(byte[]? bytes, string context = "PDF")
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException($"{context} generation returned empty data");
            }

            if (bytes.Length < 4 ||
                bytes[0] != 0x25 || // %
                bytes[1] != 0x50 || // P
                bytes[2] != 0x44 || // D
                bytes[3] != 0x46)   // F
            {
                throw new InvalidOperationException($"{context} generation returned invalid PDF data");
            }

            return bytes;
        }

        public static string SanitizeFilename(string? name, string fallback = "document")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            var sanitized = name.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }

            return sanitized.Replace(' ', '_');
        }
    }
}
