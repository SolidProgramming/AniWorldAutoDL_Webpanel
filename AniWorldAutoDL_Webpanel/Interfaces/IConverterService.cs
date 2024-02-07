using CliWrap;

namespace AniWorldAutoDL_Webpanel.Interfaces
{
    internal interface IConverterService
    {
        bool Init();
        Task<CommandResult?> StartDownload(string streamUrl, DownloadModel download, string downloadPath);
    }
}
