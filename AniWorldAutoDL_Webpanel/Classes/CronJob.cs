﻿using HtmlAgilityPack;
using PuppeteerSharp;
using Quartz;
using System.Text.RegularExpressions;
using System.Web;


namespace AniWorldAutoDL_Webpanel.Classes
{
    internal class CronJob(ILogger<CronJob> logger, IApiService apiService, IConverterService converterService, IHostApplicationLifetime appLifetime)
         : IJob
    {
        public delegate void CronJobEventHandler(CronJobState jobState);
        public static event CronJobEventHandler? CronJobEvent;

        public delegate void CronJobErrorEventHandler(MessageType messageType, string message);
        public static event CronJobErrorEventHandler? CronJobErrorEvent;

        public delegate void CronJobDownloadsEventHandler(int downloadCount, int languageDownloadCount);
        public static event CronJobDownloadsEventHandler? CronJobDownloadsEvent;
        public static CronJobState CronJobState { get; set; } = CronJobState.WaitForNextCycle;

        public static Queue<EpisodeDownloadModel>? DownloadQue;
        public static List<EpisodeDownloadModel> SkippedDownloads = [];

        public static int Interval;
        public static DateTime? NextRun = default;

        private IBrowser? Browser;

        public static int DownloadCount { get; set; }
        public static int LanguageDownloadCount { get; set; }

        private bool RegisteredShutdown { get; set; }

        private void SetCronJobState(CronJobState jobState)
        {
            CronJobState = jobState;
            logger.LogInformation($"{DateTime.Now} | {InfoMessage.CronJobChangedState} {jobState}");

            CronJobEvent?.Invoke(jobState);
        }

        private void SetCronJobDownloads(int downloadCount, int languageDownloadCount)
        {
            DownloadCount = downloadCount;
            LanguageDownloadCount = languageDownloadCount;

            CronJobDownloadsEvent?.Invoke(downloadCount, languageDownloadCount);
        }

        public async Task Execute(IJobExecutionContext context)
        {
            if (!RegisteredShutdown)
            {
                appLifetime.ApplicationStopping.Register(async () =>
                {
                    await Abort();
                });

                RegisteredShutdown = true;
            }

            NextRun = context!.NextFireTimeUtc!.Value.ToLocalTime().DateTime;
            await CheckForNewDownloads();
        }

        public async Task CheckForNewDownloads()
        {
            logger.LogInformation($"{DateTime.Now} | {CronJobState}");

            if (CronJobState != CronJobState.WaitForNextCycle)
            {
                logger.LogInformation($"{DateTime.Now} | {InfoMessage.CronJobRunning}");
                CronJobErrorEvent?.Invoke(MessageType.Info, InfoMessage.CronJobRunning);

                return;
            }

            SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

            if (settings is null || string.IsNullOrEmpty(settings.DownloadPath) || string.IsNullOrEmpty(settings.ApiUrl))
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettings}");
                CronJobErrorEvent?.Invoke(MessageType.Error, ErrorMessage.ReadSettings);
                return;
            }

            SetCronJobState(CronJobState.CheckingForDownloads);

            HosterModel? sto = HosterHelper.GetHosterByEnum(Hoster.STO);
            HosterModel? aniworld = HosterHelper.GetHosterByEnum(Hoster.AniWorld);

            bool hosterReachableSTO = await HosterHelper.HosterReachable(sto);
            bool hosterReachableAniworld = await HosterHelper.HosterReachable(aniworld);

            string? logMessage;

            if (!hosterReachableSTO)
            {
                logMessage = $"{sto.Host} {ErrorMessage.HosterUnavailable}";
                CronJobErrorEvent?.Invoke(MessageType.Error, logMessage);
            }

            if (!hosterReachableAniworld)
            {
                logMessage = $"{aniworld.Host} {ErrorMessage.HosterUnavailable}";
                CronJobErrorEvent?.Invoke(MessageType.Error, logMessage);
            }

            if (!hosterReachableSTO || !hosterReachableAniworld)
            {
                SetCronJobDownloads(0, 0);
                SetCronJobState(CronJobState.WaitForNextCycle);
                return;
            }

            SkippedDownloads.Clear();

            IEnumerable<EpisodeDownloadModel>? downloads = await apiService.GetAsync<IEnumerable<EpisodeDownloadModel>?>("getDownloads");

            if (downloads is null || !downloads.Any())
            {
                SetCronJobDownloads(0, 0);
                SetCronJobState(CronJobState.WaitForNextCycle);

                CronJobErrorEvent?.Invoke(MessageType.Info, InfoMessage.NoDownloadsInQueue);
                return;
            }

            SetCronJobState(CronJobState.Running);

            DownloadQue = downloads.EnqueueRange();
            ConverterService.CTS = new CancellationTokenSource();

            while (DownloadQue!.Count != 0)
            {
                if (ConverterService.CTS is not null && ConverterService.CTS.IsCancellationRequested && !ConverterService.AbortIsSkip)
                    break;

                EpisodeDownloadModel episodeDownload = DownloadQue.Dequeue();

                if (SkippedDownloads.Contains(episodeDownload))
                {
                    continue;
                }

                SetCronJobDownloads(DownloadQue.Count, 0);

                if (string.IsNullOrEmpty(episodeDownload.Download.Name))
                    continue;

                string originalEpisodeName = episodeDownload.Download.Name;

                string url = "";

                if (episodeDownload.StreamingPortal.Name == "S.TO")
                {
                    url = $"https://s.to/serie/stream{episodeDownload.Download.Path}/{string.Format(Globals.LinkBlueprint, episodeDownload.Download.Season, episodeDownload.Download.Episode)}";
                }
                else if (episodeDownload.StreamingPortal.Name == "AniWorld")
                {
                    url = $"https://aniworld.to/anime/stream{episodeDownload.Download.Path}/{string.Format(Globals.LinkBlueprint, episodeDownload.Download.Season, episodeDownload.Download.Episode)}";
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
                    CronJobErrorEvent?.Invoke(MessageType.Error, ex.Message);
                    hasError = true;
                    continue;
                }
                catch (Exception ex)
                {
                    CronJobErrorEvent?.Invoke(MessageType.Error, ex.Message);
                    hasError = true;
                    continue;
                }
                finally
                {
                    if (hasError)
                    {
                        CronJobErrorEvent?.Invoke(MessageType.Warning, WarningMessage.DownloadNotRemoved);
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
                    SetCronJobDownloads(DownloadQue.Count, downloadLanguages.Count() - finishedDownloadsCount);

                    if (episodeDownload.StreamingPortal.Name == "S.TO")
                    {
                        url = $"https://s.to{languageRedirectLinks[language][0]}";
                    }
                    else if (episodeDownload.StreamingPortal.Name == "AniWorld")
                    {
                        url = $"https://aniworld.to{languageRedirectLinks[language][0]}";
                    }
                    else { continue; }

                    string? m3u8Url = await GetEpisodeM3U8(url);

                    if (string.IsNullOrEmpty(m3u8Url))
                        continue;

                    episodeDownload.Download.Name = $"{originalEpisodeName.GetValidFileName()}[{language}]";

                    CommandResultExt? result = await converterService.StartDownload(m3u8Url, episodeDownload.Download, settings.DownloadPath);

                    finishedDownloadsCount++;

                    if (result is not null && result.Skipped)
                    {
                        CronJobErrorEvent?.Invoke(MessageType.Info, InfoMessage.EpisodeDownloadSkipped);

                        continue;
                    }

                    if (result is not null && result.SkippedNoResult)
                    {
                        CronJobErrorEvent?.Invoke(MessageType.Info, InfoMessage.EpisodeDownloadSkippedFileExists);

                        await RemoveDownload(episodeDownload);

                        continue;
                    }

                    if (ConverterService.CTS is not null && ( result is null || !result.IsSuccess ))
                    {
                        if (ConverterService.CTS.IsCancellationRequested)
                        {
                            logMessage = $"{WarningMessage.DownloadCanceled} {WarningMessage.DownloadNotRemoved}";
                            CronJobErrorEvent?.Invoke(MessageType.Warning, logMessage);
                            break;
                        }
                        else
                        {
                            logMessage = $"{WarningMessage.FFMPEGExecutableFail}\n{WarningMessage.DownloadNotRemoved}";
                            CronJobErrorEvent?.Invoke(MessageType.Warning, logMessage);
                        }
                    }

                    if (result is not null && result.IsSuccess)
                    {
                        CronJobErrorEvent?.Invoke(MessageType.Success, InfoMessage.DownloadFinished);

                        if (finishedDownloadsCount >= downloadLanguages.Count())
                            await RemoveDownload(episodeDownload);
                    }
                }
            }

            SetCronJobDownloads(0, 0);
            SetCronJobState(CronJobState.WaitForNextCycle);
        }

        public static void RemoveHandlers()
        {
            if (CronJobEvent is not null)
            {
                foreach (Delegate d in CronJobEvent.GetInvocationList())
                {
                    CronJobEvent -= (CronJobEventHandler)d;
                }
            }

            if (CronJobErrorEvent is not null)
            {
                foreach (Delegate d in CronJobErrorEvent.GetInvocationList())
                {
                    CronJobErrorEvent -= (CronJobErrorEventHandler)d;
                }
            }

            if (CronJobDownloadsEvent is not null)
            {
                foreach (Delegate d in CronJobDownloadsEvent.GetInvocationList())
                {
                    CronJobDownloadsEvent -= (CronJobDownloadsEventHandler)d;
                }
            }
        }

        public async Task Abort()
        {
            if (Browser is not null)
            {
                await Browser.CloseAsync();
            }
        }

        private async Task<string?> GetEpisodeM3U8(string streamUrl)
        {
            Browser ??= await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
            });

            using IPage? page = await Browser.NewPageAsync();

            try
            {
                await page.GoToAsync(streamUrl);
                await page.WaitForSelectorAsync("button.plyr__control.plyr__control--overlaid", new WaitForSelectorOptions { Timeout = 4000 });
                await page.ClickAsync("button.plyr__control.plyr__control--overlaid");
                await page.BringToFrontAsync();
                await page.WaitForSelectorAsync("button.plyr__control.plyr__control--overlaid", new WaitForSelectorOptions() { Timeout = 4000 });

                string html = await page.GetContentAsync();

                HtmlDocument htmlDocument = new();
                htmlDocument.LoadHtml(html);

                Match hlsMatch = new Regex("'hls': '(.*?)',").Match(html);

                if (hlsMatch.Success)
                {
                    return HttpUtility.HtmlDecode(hlsMatch.Groups[1].Value);
                }

                Match sourceMatch = new Regex("<source src=\"(.*?)\" type=\"application/x-mpegurl\" data-vds=\"\">").Match(html);

                if (sourceMatch.Success)
                {
                    return HttpUtility.HtmlDecode(sourceMatch.Groups[1].Value);
                }

                return default;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
                return null;
            }
            finally
            {
                await Browser.CloseAsync();
                Browser = default;
            }

        }

        private async Task RemoveDownload(EpisodeDownloadModel episodeDownload)
        {
            bool removeSuccess = await apiService.RemoveFinishedDownload(episodeDownload);

            if (removeSuccess)
            {
                CronJobErrorEvent?.Invoke(MessageType.Info, InfoMessage.DownloadDBRemoved);
            }
            else
            {
                CronJobErrorEvent?.Invoke(MessageType.Warning, WarningMessage.DownloadNotRemoved);
            }
        }
    }
}
