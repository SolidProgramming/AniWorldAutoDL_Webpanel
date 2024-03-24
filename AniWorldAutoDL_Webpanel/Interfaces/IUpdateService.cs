namespace AniWorldAutoDL_Webpanel.Interfaces
{
    public interface IUpdateService
    {
        event EventHandler OnUpdateCheckStarted;
        event EventHandler<(bool, UpdateDetailsModel?)> OnUpdateCheckFinished;

        Task CheckForUpdates();
        void DownloadUpdate(UpdateDetailsModel updateDetails);
    }
}
