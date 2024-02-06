using Newtonsoft.Json;
using System.Reflection;

namespace AniWorldAutoDL_Webpanel.Misc
{
    internal static class SettingsHelper
    {
        internal static T? ReadSettings<T>()
        {
            string path;
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                path = @"/app/appdata/settings.json";

                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                };
            }
            else
            {
                path = "settings.json";
            }

            using StreamReader r = new(path);
            string json = r.ReadToEnd();

            SettingsModel? settings = JsonConvert.DeserializeObject<SettingsModel>(json);

            if (settings is null) return default;

            if (typeof(T) == typeof(SettingsModel))
            {
                return (T)Convert.ChangeType(settings, typeof(T));
            }

            return settings.GetSetting<T>();
        }

        public static T? GetSetting<T>(this SettingsModel settings)
        {
            return (T?)settings?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .First(_ => _.PropertyType == typeof(T))
                .GetValue(settings, null);
        }
    }
}
