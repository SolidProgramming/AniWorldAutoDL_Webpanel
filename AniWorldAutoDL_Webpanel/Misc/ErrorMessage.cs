﻿namespace AniWorldAutoDL_Webpanel.Misc
{
    internal class ErrorMessage
    {
        internal const string ReadSettings = "Couldn't read settings!";
        internal const string ReadSettingsApiUrl = "Couldn't read/parse API Url!";
        internal const string WrongCredentials = "Login failed! Check credentials!";
        internal const string FFMPEGBinarieNotFound = "Couldn't find FFMPEG binaries!";
        internal const string FFProbeBinariesNotFound = "Couldn't find FFProbe binaries!";
        internal const string DBInitFailed = "DB init failed!";
        internal const string HosterUnavailable = "Hoster blocked or unavailable! Check and resolve captchas!";
        internal const string ProcessNotAssociated = "Process not associated. No need to kill.";
        internal const string APIServiceNotInitialized = "API Service is not initialized! Call IApiService.Init() on startup.";
        internal const string ConverterServiceNotInitialized = "Converter Service is not initialized! Call IConverterService.Init() on startup.";
        internal const string CronJobNotInitialized = "Cron Job/Service is not initialized! Call c on startup.";
    }
}
