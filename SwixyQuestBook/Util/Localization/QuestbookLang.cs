using Vintagestory.API.Config;

namespace SwixyQuestBook.Util.Localization
{
    public static class QuestbookLang
    {
        private const string Domain = "swixyquestbook";

        public static string Get(string key)
        {
            return Lang.Get(key);
        }

        public static string Get(string key, params object[] args)
        {
            return Lang.Get(key, args);
        }

        public static string GetLocal(string key)
        {
            return Get($"{Domain}:{key}");
        }

        public static string GetLocal(string key, params object[] args)
        {
            return Get($"{Domain}:{key}", args);
        }
    }
}
