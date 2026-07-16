namespace SwixyQuestBook.Util.Items
{
    public static class QuestbookItemCodeHelper
    {
        public static bool IsVariantWildcardCode(string? collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
                return false;

            string path = GetPath(collectibleCode);
            return path.EndsWith("-*", StringComparison.Ordinal);
        }

        public static bool SupportsVariantWildcard(string? collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode) || collectibleCode.Contains('*'))
                return false;

            return GetPath(collectibleCode).Contains('-', StringComparison.Ordinal);
        }

        public static string ToVariantWildcard(string collectibleCode)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
                return string.Empty;

            if (collectibleCode.Contains('*'))
                return collectibleCode;

            string domain = GetDomain(collectibleCode);
            string path = GetPath(collectibleCode);
            int lastDash = path.LastIndexOf('-');
            if (lastDash <= 0)
                return collectibleCode;

            string wildcardPath = path[..(lastDash + 1)] + "*";
            return string.IsNullOrEmpty(domain) ? wildcardPath : $"{domain}:{wildcardPath}";
        }

        public static string GetEffectiveCollectibleCode(string collectibleCode, bool matchAllVariants)
        {
            if (string.IsNullOrWhiteSpace(collectibleCode))
                return string.Empty;

            if (!matchAllVariants || !SupportsVariantWildcard(collectibleCode))
                return collectibleCode;

            return ToVariantWildcard(collectibleCode);
        }

        private static string GetPath(string collectibleCode)
        {
            int colonIndex = collectibleCode.IndexOf(':');
            return colonIndex >= 0 ? collectibleCode[(colonIndex + 1)..] : collectibleCode;
        }

        private static string GetDomain(string collectibleCode)
        {
            int colonIndex = collectibleCode.IndexOf(':');
            return colonIndex >= 0 ? collectibleCode[..colonIndex] : string.Empty;
        }
    }
}