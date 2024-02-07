using CliWrap;
using System.Text;
using System.Text.RegularExpressions;

namespace AniWorldAutoDL_Webpanel.Classes
{
    internal static class Converter
    {
        internal delegate void ConverterStateChangedEvent(ConverterState state);
        internal static event ConverterStateChangedEvent ConverterStateChanged;

        internal delegate void ConvertProgressChangedEvent(ConvertProgressModel convertProgress);
        internal static event ConvertProgressChangedEvent ConvertProgressChanged;

        internal delegate void ConvertStartedEvent(DownloadModel download);
        internal static event ConvertStartedEvent ConvertStarted;
        private static DownloadModel Download { get; set; }

        internal static bool FoundBinaries()
        {
            return ( File.Exists(Helper.GetFFMPEGPath()) && File.Exists(Helper.GetFFProbePath()) );
        }

        internal static async Task<CommandResult?> StartDownload(string streamUrl, DownloadModel download, string downloadPath)
        {
            ConverterStateChanged?.Invoke(ConverterState.Downloading);

            TimeSpan streamDuration = await GetStreamDuration(streamUrl);

            if (streamDuration == TimeSpan.Zero)
                return default;

            download.StreamDuration = streamDuration;

            Download = download;

            string args = $"-y -i \"{streamUrl}\" -acodec copy -vcodec copy -sn \"{GetFileName(Download, downloadPath)}\" -f matroska";

            string binPath = Helper.GetFFMPEGPath();

            ConvertStarted?.Invoke(download);

            CommandResult? result = default;

            try
            {
                result = await Cli.Wrap(binPath)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.ZeroExitCode)
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(ReadOutput, Encoding.UTF8))
                    .ExecuteAsync();
            }
            catch (OperationCanceledException)
            {
                Console.Out.WriteLine($"{DateTime.Now} | Download for {Download.Name} aborted!");
                Console.Out.WriteLine($"\n{DateTime.Now} | Press any key to close!");
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine($"{DateTime.Now} | {ex}");
            }
            finally
            {
                ConverterStateChanged?.Invoke(ConverterState.Idle);
            }

            return result;
        }

        private static string GetFileName(DownloadModel download, string downloadPath)
        {
            string folderPath;
            string seasonFolderName;
            string episodeFolderName;

            seasonFolderName = $"S{download.Season:D2}";
            episodeFolderName = $"E{download.Episode:D2}";

            folderPath = Path.Combine(downloadPath, download.Name, seasonFolderName);

            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);

            string seriesFolderPath = Path.Combine(downloadPath, download.Name);
            if (!Directory.Exists(seriesFolderPath))
                Directory.CreateDirectory(seriesFolderPath);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            return Path.Combine(folderPath, $"{download.Name}_{seasonFolderName}{episodeFolderName}.mkv");
        }

        private static void ReadOutput(string output)
        {
            if (!Regex.IsMatch(output, "time=(.*?) bitrate"))
                return;

            ConvertProgressModel progress = new();

            try
            {
                Match sizeMatch = Regex.Match(output, "size=.*?(\\d+(?:[\\.\\,]\\d+)*)kB time");

                if (!sizeMatch.Success)
                    return;

                string sizeText = sizeMatch.Groups[1].Value;
                progress.Size = float.Parse(sizeText);

                Match timeMatch = Regex.Match(output, "time=(.*?) bitrate");

                if (!timeMatch.Success)
                    return;

                string timeText = timeMatch.Groups[1].Value;
                progress.Time = TimeSpan.Parse(timeText);
                double progressPercent = 100.0d * ( progress.Time.TotalSeconds / Download.StreamDuration.TotalSeconds );

                if (progressPercent <= 0.0)
                    return;

                progress.ProgressPercent = Convert.ToInt32(progressPercent);


                Match bitrateMatch = Regex.Match(output, "bitrate=.*?(\\d+(?:[\\.\\,]\\d+)*)kbits");

                if (!bitrateMatch.Success)
                    return;

                string bitrateText = bitrateMatch.Groups[1].Value;
                progress.Bitrate = float.Parse(bitrateText);

                Match speedMatch = Regex.Match(output, "speed=.*?(\\d+(?:[\\.\\,]\\d+)*)");

                if (!speedMatch.Success)
                    return;

                string speedText = speedMatch.Groups[1].Value;
                progress.Speed = float.Parse(speedText);

                Match fpsMatch = Regex.Match(output, "fps=.*?(\\d+(?:[\\.\\,]\\d+)*)");

                if (!fpsMatch.Success)
                    return;

                string fpsText = fpsMatch.Groups[1].Value;
                progress.FPS = float.Parse(fpsText);

                ConvertProgressChanged?.Invoke(progress);
            }
            catch (Exception) { }
        }

        private static async Task<TimeSpan> GetStreamDuration(string streamUrl)
        {
            StringBuilder stdOutBuffer = new();

            string args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -sexagesimal \"{streamUrl}\"";

            string binPath = Helper.GetFFProbePath();

            CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));

            try
            {
                await Cli.Wrap(binPath)
                .WithArguments(args)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                        .ExecuteAsync(cts.Token);
            }catch(OperationCanceledException)
            {
                Console.Out.WriteLine($"{DateTime.Now} | Timeout | Retry on next cycle");
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine($"{DateTime.Now} | {ex}");
                return TimeSpan.Zero;
            }

            string? stdOut = stdOutBuffer.ToString();

            if (string.IsNullOrEmpty(stdOut))
                return TimeSpan.Zero;

            return TimeSpan.Parse(stdOut);
        }
    }
}
