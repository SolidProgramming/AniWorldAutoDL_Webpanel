namespace AniWorldAutoDL_Webpanel.Interfaces
{
    public interface IUpdateService
    {
        Task<(bool, UpdateDetailsModel?)> CheckForUpdates(string AssemblyVersion);
        void DownloadUpdate(UpdateDetailsModel updateDetails);
    }
}
