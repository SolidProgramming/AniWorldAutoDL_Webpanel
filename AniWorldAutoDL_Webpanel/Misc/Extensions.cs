using Quartz;
using System.Collections;
using System.Text.RegularExpressions;
using System.Linq;

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

            DateTimeOffset startTime = new DateTimeOffset(DateTime.Now.ToLocalTime())
                                                .AddSeconds(10);

            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity(jobName + "-trigger")
                .WithSimpleSchedule(_ =>
                    _.WithIntervalInMinutes(intervalInMinutes)
                    .RepeatForever())
                .StartAt(startTime));

            CronJob.NextRun = startTime.DateTime;
            CronJob.Interval = intervalInMinutes;            
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

        internal static string? ToVOELanguageKey(this Language language)
        {
            if (VOELanguageKeyCollection.ContainsKey(language))
            {
                return VOELanguageKeyCollection[language];
            }

            return default;
        }

        internal static string Repeat(this string text, int n)
        {
            ReadOnlySpan<char> textAsSpan = text.AsSpan();
            Span<char> span = new(new char[textAsSpan.Length * n]);
            for (var i = 0; i < n; i++)
            {
                textAsSpan.CopyTo(span.Slice(i * textAsSpan.Length, textAsSpan.Length));
            }

            return span.ToString();
        }

        public static Queue<T>? EnqueueRange<T>(this IEnumerable<T> source) where T : class
        {
            if (!source.Any())
                return default;

            Queue<T> que = new();

            source.ToList().ForEach(que.Enqueue);

            return que;
        }

        public static IEnumerable<T> GetFlags<T>(this Enum input, Enum? ignore = default)
        {
            foreach (T value in Enum.GetValues(input.GetType()))
            {
                Enum? enumVal = (Enum)Convert.ChangeType(value, typeof(Enum));
                if (!enumVal.Equals(ignore) && input.HasFlag(enumVal))
                    yield return value;
            }
        }
    }
}
