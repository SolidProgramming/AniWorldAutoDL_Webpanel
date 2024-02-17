namespace AniWorldAutoDL_Webpanel.Misc
{
    internal static class Helper
    {
        internal static string GetFFMPEGPath()
        {
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
                return Path.Combine(Directory.GetCurrentDirectory(), "appdata", Globals.FFMPEGBinDocker);

            return Path.Combine(Directory.GetCurrentDirectory(), Globals.FFMPEGBin);
        }            

        internal static string? GetFFProbePath()
        {           
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
                return Path.Combine(Directory.GetCurrentDirectory(), "appdata", Globals.FFProbeBinDocker);

            return Path.Combine(Directory.GetCurrentDirectory(), Globals.FFProbeBin);
        }

    }
}
