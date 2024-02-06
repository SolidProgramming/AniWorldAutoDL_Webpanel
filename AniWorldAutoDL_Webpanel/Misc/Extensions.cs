﻿using Quartz;
using System.Text.RegularExpressions;

namespace AniWorldAutoDL_Webpanel.Misc
{
    internal static class Extensions
    {
        internal static void AddJobAndTrigger<T>(this IServiceCollectionQuartzConfigurator quartz, int intervalInMinutes) where T : IJob
        {
            // Use the name of the IJob as the appsettings.json key
            string jobName = typeof(T).Name;

            // Try and load the schedule from configuration
            var configKey = $"Quartz:{jobName}";

            // register the job as before
            JobKey? jobKey = new(jobName);
            quartz.AddJob<T>(opts => opts.WithIdentity(jobKey));

            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity(jobName + "-trigger")
                .WithSimpleSchedule(_ =>
                    _.WithIntervalInMinutes(intervalInMinutes)
                    .RepeatForever()));
        }

        private static Dictionary<Language, string> VOELanguageKeyCollection = new()
        {
            { Language.GerDub, "1"},
            { Language.GerSub, "3"},
            { Language.EngDub, "2"},
            { Language.EngSub, "2"},
        };

        internal static string GetValidFileName(this string name)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(name, "");
        }

        internal static string UrlSanitize(this string text)
        {
            return text.Replace(' ', '-')
                .Replace(":", "")
                .Replace("~", "")
                .Replace("'", "")
                .Replace(",", "")
                .Replace("’", "");
        }

        internal static string ToVOELanguageKey(this Language language)
        {
            if (VOELanguageKeyCollection.ContainsKey(language))
            {
                return VOELanguageKeyCollection[language];
            }

            return null;
        }
    }
}
