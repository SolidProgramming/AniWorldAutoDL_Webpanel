using System.Xml.Serialization;
using Updater.Interfaces;
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

        public void DownloadUpdate(UpdateDetailsModel updateDetails)
        {
            throw new NotImplementedException();
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
