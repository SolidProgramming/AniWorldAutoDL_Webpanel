namespace AniWorldAutoDL_Webpanel.Misc
{
    internal static class Helper
    {
        internal static string GetFFMPEGPath()
            => Path.Combine(Directory.GetCurrentDirectory(), Globals.FFMPEGBin);

        internal static string GetFFProbePath()
            => Path.Combine(Directory.GetCurrentDirectory(), Globals.FFProbeBin);
    }
}
