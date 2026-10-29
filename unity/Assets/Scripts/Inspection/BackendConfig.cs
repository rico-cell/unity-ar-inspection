using UnityEngine;

namespace Inspection
{
    // The on-screen URL is stored in PlayerPrefs so a device build can point at
    // the computer running Go without another Unity rebuild.
    public static class BackendConfig
    {
        public const string DefaultBaseUrl = "http://192.168.0.10:8787";
        public const string PrefKey = "inspection.backendBaseUrl";

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";
            raw = raw.Trim();
            if (!raw.StartsWith("http://") && !raw.StartsWith("https://"))
                raw = "http://" + raw;
            while (raw.EndsWith("/"))
                raw = raw.Substring(0, raw.Length - 1);
            return raw;
        }

        public static string Load(string inspectorDefault)
        {
            if (PlayerPrefs.HasKey(PrefKey))
            {
                var saved = Normalize(PlayerPrefs.GetString(PrefKey));
                if (saved.Length > 0)
                    return saved;
            }

            var fallback = Normalize(inspectorDefault);
            if (fallback.Length > 0)
                return fallback;
            return DefaultBaseUrl;
        }

        public static void Save(string raw)
        {
            var url = Normalize(raw);
            if (url.Length == 0)
                return;
            PlayerPrefs.SetString(PrefKey, url);
            PlayerPrefs.Save();
        }
    }
}
