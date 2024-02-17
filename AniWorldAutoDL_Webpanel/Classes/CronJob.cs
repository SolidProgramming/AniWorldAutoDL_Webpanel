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

            string errorMessage = $"{DateTime.Now} | ";

            SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

            if (settings is null || string.IsNullOrEmpty(settings.DownloadPath) || string.IsNullOrEmpty(settings.User.Username) || string.IsNullOrEmpty(settings.User.Password))
            {
                errorMessage += ErrorMessage.ReadSettings;

                logger.LogError(errorMessage);
                CronJobErrorEvent?.Invoke(Severity.Error, errorMessage);
                return;
            }

            HosterModel? sto = HosterHelper.GetHosterByEnum(Hoster.STO);
            HosterModel? aniworld = HosterHelper.GetHosterByEnum(Hoster.AniWorld);

            bool hosterReachableSTO = await HosterHelper.HosterReachable(sto);
            bool hosterReachableAniworld = await HosterHelper.HosterReachable(aniworld);

            if (!hosterReachableSTO)
            {
                errorMessage += $"{sto.Hoster} {ErrorMessage.HosterUnavailable}";

                logger.LogError(errorMessage);
                CronJobErrorEvent?.Invoke(Severity.Error, errorMessage);
                return;
            }

            if (!hosterReachableAniworld)
            {
                errorMessage += $"{aniworld.Hoster} {ErrorMessage.HosterUnavailable}";

                logger.LogError(errorMessage);
                CronJobErrorEvent?.Invoke(Severity.Error, errorMessage);
                return;
            }

            bool loginSuccess = await apiService.Login(settings.User.Username, settings.User.Password);

            if (!loginSuccess)
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.WrongCredentials}");
                return;
            }

            IEnumerable<EpisodeDownloadModel>? downloads = await apiService.GetAsync<IEnumerable<EpisodeDownloadModel>?>("getDownloads", new() { { "username", settings.User.Username } });

            if (downloads is not null && downloads.Any())
            {
                CronJobEvent?.Invoke(CronJobState.Running, downloads.Count());
            }

            foreach (EpisodeDownloadModel episodeDownload in downloads)
            {
                if (ConverterService.CTS is not null && ConverterService.CTS.IsCancellationRequested)
                    return;

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
                        logger.LogWarning($"{DateTime.Now} | {WarningMessage.DownloadNotRemoved}");
                    }
                    else
                    {
                        logger.LogWarning($"{DateTime.Now} | {WarningMessage.FFMPEGExecutableFail}\n{WarningMessage.DownloadNotRemoved}");
                    }
                }
                
                if (result is not null && result.IsSuccess)
                {
                    bool removeSuccess = await apiService.RemoveFinishedDownload(episodeDownload.Download.Id.ToString());

                    if (removeSuccess)
                    {
                        logger.LogInformation($"{DateTime.Now} | {InfoMessage.DownloadFinished} {InfoMessage.DownloadDBRemoved}");
                    }
                    else
                    {
                        logger.LogWarning($"{DateTime.Now} | {WarningMessage.DownloadNotRemoved}");
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
