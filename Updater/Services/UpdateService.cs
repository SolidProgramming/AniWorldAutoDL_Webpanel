using System.Xml.Serialization;
using Updater.Interfaces;
using Updater.Misc;
using Updater.Models;

namespace Updater.Services
{
    public class UpdateService : IUpdateService
    {        
        public event EventHandler? OnUpdateCheckStarted;
        public event EventHandler<(bool, UpdateDetailsModel?)>? OnUpdateCheckFinished;

        private UpdateDetailsModel? UpdateDetails;
        private bool UpdateAvailable;

        private const string UpdatesDetailsUrl = "https://autoupdate.solidserver.xyz/autoupdater_aniworldautodl/latest.xml";
        private const string UpdatesLatestUrl = "https://autoupdate.solidserver.xyz/autoupdater_aniworldautodl/updates/latest.zip";
             
        public async Task CheckForUpdates(string assemblyVersion)
        {
            if (UpdateAvailable && UpdateDetails is not null)
                OnUpdateCheckFinished?.Invoke(this, (true, UpdateDetails));

            OnUpdateCheckStarted?.Invoke(this, EventArgs.Empty);

            await Task.Delay(2000);

            using HttpClient client = new();

            using CancellationTokenSource cts = new();
            cts.CancelAfter(2000);

            try
            {
                string result = await client.GetStringAsync(UpdatesDetailsUrl, cts.Token);

                UpdateDetailsModel? updateDetails;

                if (result.Length > 0)
                {
                    updateDetails = ParseUpdateModel(result);

                    if (updateDetails is null)
                    {
                        OnUpdateCheckFinished?.Invoke(this, (false, default));
                        return;
                    }

                    if (new Version(updateDetails.Version) > new Version(assemblyVersion))
                    {
                        UpdateAvailable = true;
                        UpdateDetails = updateDetails;
                        OnUpdateCheckFinished?.Invoke(this, (UpdateAvailable, UpdateDetails));
                        return;
                    }      
                }

                OnUpdateCheckFinished?.Invoke(this, (false, default));
            }
            catch (Exception)
            {
                return;
            }
        }

        public async Task DownloadUpdate(UpdateDetailsModel updateDetails, IProgress<float> progress)
        {
            using HttpClient? client = new();

            string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "updates");

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string filePath = Path.Combine(directoryPath, "latest.zip");    

            using FileStream? file = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                                  
            CancellationTokenSource cts = new();
            CancellationToken cancellationToken = cts.Token;            

            await client.DownloadAsync(UpdatesLatestUrl, file, progress, cancellationToken);
        }

        private static UpdateDetailsModel? ParseUpdateModel(string xmlData)
        {
            try
            {
                XmlSerializer? serializer = new(typeof(UpdateDetailsModel));
                StringReader? rdr = new(xmlData);

                return Convert.ChangeType(serializer.Deserialize(rdr), typeof(UpdateDetailsModel), System.Globalization.CultureInfo.InvariantCulture) as UpdateDetailsModel;
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}
