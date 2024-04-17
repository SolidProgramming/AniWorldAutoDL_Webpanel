using CliWrap;

namespace AniWorldAutoDL_Webpanel.Interfaces
{
    internal interface IConverterService
    {
        bool Init();
        Task<CommandResultExt?> StartDownload(string streamUrl, DownloadModel download, string downloadPath, DownloaderPreferencesModel downloaderPreferences);
    }
}
