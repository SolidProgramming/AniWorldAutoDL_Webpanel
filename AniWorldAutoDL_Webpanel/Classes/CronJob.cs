using AniWorldAutoDL_Webpanel.Misc;
using CliWrap;
using Microsoft.Extensions.Primitives;
using Quartz;
using Telegram.Bot.Types;

namespace AniWorldAutoDL_Webpanel.Classes
{
    [DisallowConcurrentExecution]
    internal class CronJob(ILogger<CronJob> logger, IApiService apiService, IConverterService converterService)
         : IJob
    {
        public delegate void CronJobEventHandler(CronJobState jobState, int downloadCount = 0);
        public static event CronJobEventHandler? CronJobEvent;

        public delegate void CronJobErrorEventHandler(Severity severity, string message);
        public static event CronJobErrorEventHandler? CronJobErrorEvent;

        private static CronJobState CronJobState { get; set; } = CronJobState.WaitForNextCycle;

        public static int Interval;
        public static DateTime? NextRun = default;

        private void CronJob_CronJobEvent(CronJobState jobState, int downloadCount = 0)
        {
            CronJobState = jobState;
            logger.LogInformation($"{DateTime.Now} | {InfoMessage.CronJobChangedState} {jobState}");
        }

        public async Task Execute(IJobExecutionContext context)
        {
            NextRun = context!.NextFireTimeUtc!.Value.ToLocalTime().DateTime;
            await CheckForNewDownloads();
        }

        public async Task CheckForNewDownloads()
        {
            if (CronJobEvent is null)
            {
                CronJobEvent += CronJob_CronJobEvent;
            }

            CronJobEvent?.Invoke(CronJobState.CheckForDownloads);

            string logMessage = $"{DateTime.Now} | ";

            SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

            if (settings is null || string.IsNullOrEmpty(settings.DownloadPath) || string.IsNullOrEmpty(settings.User.Username) || string.IsNullOrEmpty(settings.User.Password))
            {
                logMessage += ErrorMessage.ReadSettings;

                logger.LogError(logMessage);
                CronJobErrorEvent?.Invoke(Severity.Error, logMessage);
                return;
            }

            HosterModel? sto = HosterHelper.GetHosterByEnum(Hoster.STO);
            HosterModel? aniworld = HosterHelper.GetHosterByEnum(Hoster.AniWorld);

            bool hosterReachableSTO = await HosterHelper.HosterReachable(sto);
            bool hosterReachableAniworld = await HosterHelper.HosterReachable(aniworld);

            if (!hosterReachableSTO)
            {
                logMessage += $"{sto.Host} {ErrorMessage.HosterUnavailable}";
                CronJobErrorEvent?.Invoke(Severity.Error, logMessage);
                return;
            }

            if (!hosterReachableAniworld)
            {
                logMessage += $"{aniworld.Host} {ErrorMessage.HosterUnavailable}";
                CronJobErrorEvent?.Invoke(Severity.Error, logMessage);
                return;
            }

            bool loginSuccess = await apiService.Login(settings.User.Username, settings.User.Password);

            if (!loginSuccess)
            {
                logMessage += ErrorMessage.WrongCredentials;
                CronJobErrorEvent?.Invoke(Severity.Error, logMessage);
                return;
            }

            IEnumerable<EpisodeDownloadModel>? downloads = await apiService.GetAsync<IEnumerable<EpisodeDownloadModel>?>("getDownloads", new() { { "username", settings.User.Username } });

            if (downloads is not null && downloads.Any())
            {
                CronJobEvent?.Invoke(CronJobState.Running, downloads.Count());
                ConverterService.CTS = new CancellationTokenSource();
            }

            foreach (EpisodeDownloadModel episodeDownload in downloads)
            {
                if (ConverterService.CTS is not null && ConverterService.CTS.IsCancellationRequested)
                {
                    CronJobEvent?.Invoke(CronJobState.WaitForNextCycle);
                    return;
                }                    

                if (string.IsNullOrEmpty(episodeDownload.Download.Name))
                    continue;

                string url = "";

                if (episodeDownload.StreamingPortal.Name == "S.TO")
                {
                    url = $"https://s.to/serie/stream/{episodeDownload.Download.Name.UrlSanitize()}/{string.Format(Globals.LinkBlueprint, episodeDownload.Download.Season, episodeDownload.Download.Episode)}";
                }
                else if (episodeDownload.StreamingPortal.Name == "AniWorld")
                {
                    url = $"https://aniworld.to/anime/stream/{episodeDownload.Download.Name.UrlSanitize()}/{string.Format(Globals.LinkBlueprint, episodeDownload.Download.Season, episodeDownload.Download.Episode)}";
                }
                else { continue; }

                using HttpClient client = new();

                string? html = await client.GetStringAsync(url);

                Dictionary<Language, List<string>> languageRedirectLinks = HosterHelper.GetLanguageRedirectLinks(html);

                if (languageRedirectLinks == null || languageRedirectLinks.Count == 0 || !languageRedirectLinks.ContainsKey(Language.GerDub))
                    continue;

                if (episodeDownload.StreamingPortal.Name == "S.TO")
                {
                    url = $"https://s.to{languageRedirectLinks[Language.GerDub][0]}";
                }
                else if (episodeDownload.StreamingPortal.Name == "AniWorld")
                {
                    url = $"https://aniworld.to{languageRedirectLinks[Language.GerDub][0]}";
                }
                else { continue; }

                string? m3u8Url = await HosterHelper.GetEpisodeM3U8(url);

                if (string.IsNullOrEmpty(m3u8Url))
                    continue;

                episodeDownload.Download.Name = episodeDownload.Download.Name.GetValidFileName();

                CommandResult? result = await converterService.StartDownload(m3u8Url, episodeDownload.Download, settings.DownloadPath);

                if (ConverterService.CTS is not null && ( result is null || !result.IsSuccess ))
                {
                    if (ConverterService.CTS.IsCancellationRequested)
                    {
                        logMessage += WarningMessage.DownloadNotRemoved;
                        CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);
                    }
                    else
                    {
                        logMessage += $"{WarningMessage.FFMPEGExecutableFail}\n{WarningMessage.DownloadNotRemoved}";
                        CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);
                    }
                }
                
                if (result is not null && result.IsSuccess)
                {
                    bool removeSuccess = await apiService.RemoveFinishedDownload(episodeDownload.Download.Id.ToString());

                    if (removeSuccess)
                    {
                        logMessage += $"{InfoMessage.DownloadFinished} {InfoMessage.DownloadDBRemoved}";                       
                        CronJobErrorEvent?.Invoke(Severity.Information, logMessage);
                    }
                    else
                    {
                        logMessage += WarningMessage.DownloadNotRemoved;
                        CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);
                    }
                }
            }

            CronJobEvent?.Invoke(CronJobState.WaitForNextCycle);
        }

        public static CronJobState GetCronJobState()
        {
            return CronJobState;
        }

    }
}
