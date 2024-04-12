using System.ComponentModel.DataAnnotations;

namespace AniWorldAutoDL_Webpanel.Models
{
    public class DownloaderPreferencesModel
    {
        [Required(ErrorMessageResourceType = typeof(int), ErrorMessage = "Bitte eine Zahl eingeben")]
        public int Interval { get; set; }

        public bool AutoStart { get; set; }

        public bool TelegramCaptchaNotification { get; set; }
    }
}
