using Newtonsoft.Json.Linq;

namespace UserReplay
{
    public static class Utils
    {
        public static string GetJPath(this JObject root, JToken token)
        {
            if (token == null)
            {
                return null;
            }
            if (token == root)
            {
                return "$";
            }
            if (!root.Descendants().Contains(token))
            {
                return null;
            }
            var tokenPath = token.Path;
            var rootPath = root.Path;
            if (tokenPath.StartsWith(rootPath))
            {
                return "$" + tokenPath[rootPath.Length..];
            }
            return null;
        }

        public static JToken SelectByJPath(this JObject root, string jpath)
        {
            IEnumerable<JToken> selected = root.SelectTokens(jpath);
            if (!selected.Any())
            {
                return JValue.CreateNull();
            }
            else if (selected.Count() > 1)
            {
                return new JArray(selected);
            }
            else
            {
                return selected.First();
            }
        }
    }
}