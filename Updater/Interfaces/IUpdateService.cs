using Updater.Models;

namespace Updater.Interfaces
{
    public interface IUpdateService
    {
        event EventHandler OnUpdateCheckStarted;
        event EventHandler<(bool, UpdateDetailsModel?)> OnUpdateCheckFinished;

        Task CheckForUpdates(string assemblyVersion);
        void DownloadUpdate(UpdateDetailsModel updateDetails);
    }
}