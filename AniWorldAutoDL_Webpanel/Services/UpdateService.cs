using System.Xml.Serialization;

namespace AniWorldAutoDL_Webpanel.Services
{
    public class UpdateService : IUpdateService
    {
        private const string UpdatesDetailsUrl = "https://autoupdate.solidserver.xyz/autoupdater_aniworldautodl/latest.xml";

        public async Task<(bool, UpdateDetailsModel?)> CheckForUpdates(string AssemblyVersion)
        {
            using HttpClient client = new();

            using CancellationTokenSource cts = new();
            cts.CancelAfter(2000);

            try
            {
                string result = await client.GetStringAsync(UpdatesDetailsUrl, cts.Token);

                if (result.Length > 0)
                {
                    UpdateDetailsModel? updateDetails = ParseUpdateModel(result);

                    if (updateDetails is null)
                        return (false, default);

                    if (new Version(updateDetails.Version) > new Version(AssemblyVersion))
                    {
                        return (true, updateDetails);
                    }
                }
            }
            catch (Exception)
            {
                return default;
            }

            return default;
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
