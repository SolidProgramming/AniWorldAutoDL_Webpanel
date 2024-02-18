using CliWrap;
using Quartz;


namespace AniWorldAutoDL_Webpanel.Classes
{
    [DisallowConcurrentExecution]
    internal class CronJob(ILogger<CronJob> logger, IApiService apiService, IConverterService converterService)
         : IJob
    {
        public delegate void CronJobEventHandler(CronJobState jobState, int downloadCount = 0, int languageDownloadCount = 0, bool forceUpdate = false);
        public static event CronJobEventHandler? CronJobEvent;

        public delegate void CronJobErrorEventHandler(Severity severity, string message);
        public static event CronJobErrorEventHandler? CronJobErrorEvent;

        private static CronJobState CronJobState { get; set; } = CronJobState.WaitForNextCycle;

        public static int Interval;
        public static DateTime? NextRun = default;

        private void CronJob_CronJobEvent(CronJobState jobState, int downloadCount = 0, int languageDownloadCount = 0, bool forceUpdate = false)
        {
            if (CronJobState == jobState && !forceUpdate)
                return;

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

            string logMessage = $"{DateTime.Now} | ";

            if (CronJobState != CronJobState.WaitForNextCycle)
            {
                logMessage += $"{InfoMessage.CronJobRunning}";

                logger.LogInformation(logMessage);
                CronJobErrorEvent?.Invoke(Severity.Information, logMessage);

                return;
            }

            CronJobEvent?.Invoke(CronJobState.CheckForDownloads);

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
            Queue<EpisodeDownloadModel>? downloadQue;

            if (downloads is null || !downloads.Any())
            {
                CronJobEvent?.Invoke(CronJobState.WaitForNextCycle);
                return;
            }

            downloadQue = downloads.EnqueueRange();
            ConverterService.CTS = new CancellationTokenSource();

            while (downloadQue?.Count != 0)
            {
                if (ConverterService.CTS is not null && ConverterService.CTS.IsCancellationRequested)
                    break;                

                logMessage = $"{DateTime.Now} | ";

                EpisodeDownloadModel episodeDownload = downloadQue.Dequeue();

                string originalEpisodeName = episodeDownload.Download.Name;

                CronJobEvent?.Invoke(CronJobState.Running, downloadQue.Count);

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

                string? html;
                bool hasError = false;

                try
                {
                    html = await client.GetStringAsync(url);
                }
                catch (HttpRequestException ex)
                {
                    logMessage += $"{ex.Message}";
                    CronJobErrorEvent?.Invoke(Severity.Error, logMessage);
                    hasError = true;
                    continue;
                }
                catch (Exception ex)
                {
                    logMessage += $"{ex.Message}";
                    CronJobErrorEvent?.Invoke(Severity.Error, logMessage);
                    hasError = true;
                    continue;
                }
                finally
                {
                    if (hasError)
                    {
                        logMessage = $"{DateTime.Now} | ";
                        logMessage += $"{WarningMessage.DownloadNotRemoved}";
                        CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);
                    }
                }

                Dictionary<Language, List<string>> languageRedirectLinks = HosterHelper.GetLanguageRedirectLinks(html);

                if (languageRedirectLinks == null || languageRedirectLinks.Count == 0)
                    continue;

                episodeDownload.Download.Name = episodeDownload.Download.Name.GetValidFileName();

                IEnumerable<Language> episodeLanguages = episodeDownload.Download.LanguageFlag.GetFlags<Language>(ignore: Language.None);
                IEnumerable<Language> redirectLanguages = languageRedirectLinks.Keys.Where(_ => episodeLanguages.Contains(_));

                IEnumerable<Language>? downloadLanguages = episodeLanguages.Intersect(redirectLanguages);

                int finishedDownloadsCount = 1;

                foreach (Language language in downloadLanguages)
                {
                    CronJobEvent?.Invoke(CronJobState.Running, downloadQue.Count, downloadLanguages.Count() - finishedDownloadsCount);

                    logMessage = $"{DateTime.Now} | ";

                    if (episodeDownload.StreamingPortal.Name == "S.TO")
                    {
                        url = $"https://s.to{languageRedirectLinks[language][0]}";
                    }
                    else if (episodeDownload.StreamingPortal.Name == "AniWorld")
                    {
                        url = $"https://aniworld.to{languageRedirectLinks[language][0]}";
                    }
                    else { continue; }

                    string? m3u8Url = await HosterHelper.GetEpisodeM3U8(url);

                    if (string.IsNullOrEmpty(m3u8Url))
                        continue;

                    episodeDownload.Download.Name = $"{originalEpisodeName.GetValidFileName()}[{language}]";

                    CommandResult? result = await converterService.StartDownload(m3u8Url, episodeDownload.Download, settings.DownloadPath);

                    finishedDownloadsCount++;

                    if (ConverterService.CTS is not null && ( result is null || !result.IsSuccess ))
                    {
                        if (ConverterService.CTS.IsCancellationRequested)
                        {                            
                            logMessage += $"{WarningMessage.DownloadCanceled} {WarningMessage.DownloadNotRemoved}";
                            CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);

                            break;
                        }
                        else
                        {
                            logMessage += $"{WarningMessage.FFMPEGExecutableFail}\n{WarningMessage.DownloadNotRemoved}";
                            CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);
                        }
                    }

                    if (result is not null && result.IsSuccess)
                    {
                        logMessage += InfoMessage.DownloadFinished;
                        CronJobErrorEvent?.Invoke(Severity.Information, logMessage);

                        if (finishedDownloadsCount >= downloadLanguages.Count())
                        {
                            bool removeSuccess = await apiService.RemoveFinishedDownload(episodeDownload.Download.Id.ToString());

                            logMessage = $"{DateTime.Now} | ";

                            if (removeSuccess)
                            {
                                logMessage += InfoMessage.DownloadDBRemoved;
                                CronJobErrorEvent?.Invoke(Severity.Information, logMessage);
                            }
                            else
                            {
                                logMessage += WarningMessage.DownloadNotRemoved;
                                CronJobErrorEvent?.Invoke(Severity.Warning, logMessage);
                            }
                        }
                    }
                }
            }

            CronJobEvent?.Invoke(CronJobState.WaitForNextCycle, 0, 0, true);
        }

        public static CronJobState GetCronJobState()
        {
            return CronJobState;
        }

    }
}
