using CliWrap;
using Quartz;

namespace AniWorldAutoDL_Webpanel.Classes
{
    [DisallowConcurrentExecution]
    internal class CronJob(ILogger<CronJob> logger, IApiService apiService, IConverterService converterService)
         : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();            

            if (settings is null || string.IsNullOrEmpty(settings.DownloadPath) || string.IsNullOrEmpty(settings.User.Username) || string.IsNullOrEmpty(settings.User.Password))
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettings}");
                return;
            }

            bool loginSuccess = await apiService.Login(settings.User.Username, settings.User.Password);

            if (!loginSuccess)
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.WrongCredentials}");
                return;
            }

            IEnumerable<EpisodeDownloadModel>? downloads = await apiService.GetAsync<IEnumerable<EpisodeDownloadModel>?>("getDownloads", new() { { "username", settings.User.Username } });

            foreach (EpisodeDownloadModel episodeDownload in downloads)
            {
                //if (token.IsCancellationRequested)
                //    return;

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

                episodeDownload.Download.Name = $"{episodeDownload.Download.Name.GetValidFileName()}[GerDub]";

                CommandResult? result = await converterService.StartDownload(m3u8Url, episodeDownload.Download, settings.DownloadPath);

                //if (token.IsCancellationRequested || result?.ExitCode != 0)
                //    continue;
            }

        }
    }
}
