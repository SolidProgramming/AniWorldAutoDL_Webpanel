using CliWrap;

namespace AniWorldAutoDL_Webpanel.Models
{
    public class CommandResultExt(int exitCode = 0, DateTimeOffset startTime = default, DateTimeOffset exitTime = default, bool skippedNoResult = false) : CommandResult(exitCode, startTime, exitTime)
    {
        public bool SkippedNoResult { get; internal set; } = skippedNoResult;
    }
}
