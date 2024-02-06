using Newtonsoft.Json;

namespace AniWorldAutoDL_Webpanel.Models
{
    internal class SettingsModel
    {
        [JsonProperty("ApiUrl")]
        public string ApiUrl { get; set; } = default!;

        [JsonProperty("User")]
        public UserModel User { get; set; } = default!;

        [JsonProperty("DownloadPath")]
        public string DownloadPath { get; set; } = default!;

    }
}
