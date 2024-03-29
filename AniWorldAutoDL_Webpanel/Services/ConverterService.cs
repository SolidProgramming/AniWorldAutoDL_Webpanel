﻿using CliWrap;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AniWorldAutoDL_Webpanel.Services
{
    public class ConverterService(ILogger<ConverterService> logger, IHostApplicationLifetime appLifetime)
        : IConverterService
    {
        public delegate void ConverterStateChangedEvent(ConverterState state, DownloadModel? download = default);
        public static event ConverterStateChangedEvent? ConverterStateChanged;

        public delegate void ConvertProgressChangedEvent(ConvertProgressModel convertProgress);
        public static event ConvertProgressChangedEvent? ConvertProgressChanged;

        public delegate void ConvertStartedEvent(DownloadModel download);
        public static event ConvertStartedEvent? ConvertStarted;
        private static DownloadModel? Download { get; set; }
        private static ConverterState ConverterState { get; set; } = ConverterState.Undefined;

        private bool IsInitialized { get; set; }
        public static bool AbortIsSkip { get; set; }

        public static CancellationTokenSource? CTS { get; set; }

        public bool Init()
        {
            appLifetime.ApplicationStopping.Register(() =>
            {
                Abort();
            });

            if (!File.Exists(Helper.GetFFMPEGPath()))
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.FFMPEGBinarieNotFound}");
                return false;
            }

            if (!File.Exists(Helper.GetFFProbePath()))
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.FFProbeBinariesNotFound}");
                return false;
            }

            ConverterStateChanged += ConverterService_ConverterStateChanged;

            IsInitialized = true;

            logger.LogInformation($"{DateTime.Now} | {InfoMessage.ConverterServiceInit}");

            return true;
        }

        private void ConverterService_ConverterStateChanged(ConverterState state, DownloadModel? download = null)
        {
            ConverterState = state;
            logger.LogInformation($"{DateTime.Now} | {InfoMessage.ConverterChangedState} {state}");
        }

        public async Task<CommandResultExt?> StartDownload(string streamUrl, DownloadModel download, string downloadPath)
        {
            if (!IsInitialized)
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.ConverterServiceNotInitialized}");
                return default;
            }

            string filePath = GetFileName(download, downloadPath);

            TimeSpan streamDuration = await GetStreamDuration(streamUrl);

            if (streamDuration == TimeSpan.Zero)
                return default;

            download.StreamDuration = streamDuration;

            if (File.Exists(filePath))
            {
                TimeSpan durationExistingFile = TimeSpan.Zero;
                durationExistingFile = await GetStreamDurationFromFile(filePath);

                if (durationExistingFile.Minutes == streamDuration.Minutes && durationExistingFile.Seconds == streamDuration.Seconds)
                {
                    logger.LogInformation($"{DateTime.Now} | {InfoMessage.EpisodeDownloadSkippedFileExists}");

                    return new CommandResultExt(skippedNoResult: true);
                }
            }

            Download = download;

            ConverterStateChanged?.Invoke(ConverterState.Downloading, download);

            string args = $"-y -i \"{streamUrl}\" -acodec copy -vcodec copy -sn \"{filePath}\" -f matroska";

            string binPath = Helper.GetFFMPEGPath();

            ConvertStarted?.Invoke(download);

            Download.DownloadStartTime = DateTime.Now;

            CTS = new CancellationTokenSource();

            CommandResultExt? result = default;

            try
            {
                CommandResult tempResult = await Cli.Wrap(binPath)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.ZeroExitCode)
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(ReadOutput, Encoding.UTF8))
                    .ExecuteAsync(CTS.Token);

                result = new CommandResultExt(tempResult.ExitCode, tempResult.StartTime, tempResult.ExitTime);
            }
            catch (OperationCanceledException)
            {
                if (AbortIsSkip)
                {
                    return new CommandResultExt(skipped: true);
                }
                else
                {
                    logger.LogWarning($"{DateTime.Now} | {WarningMessage.DownloadCanceled}");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{DateTime.Now} | {ex}");
            }
            finally
            {
                Download = default;
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

                progress.ETA = EstimateCompletionTime(progress.ProgressPercent, Download.DownloadStartTime);

                ConvertProgressChanged?.Invoke(progress);
            }
            catch (Exception) { }
        }

        private static TimeSpan EstimateCompletionTime(double completedPercentage, DateTime downloadStartTime)
        {
            TimeSpan elapsedTime = DateTime.Now.Subtract(downloadStartTime);            

            double remainingPercentage = 100 - completedPercentage;
            double estimatedTotalTime = elapsedTime.TotalSeconds / completedPercentage * 100;
            double remainingTime = estimatedTotalTime * remainingPercentage / 100;
            TimeSpan remainingTimeSpan = TimeSpan.FromSeconds(remainingTime);

            return remainingTimeSpan;
        }

        private static async Task<TimeSpan> GetStreamDuration(string streamUrl)
        {
            StringBuilder stdOutBuffer = new();

            string args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -sexagesimal \"{streamUrl}\"";

            string? binPath = Helper.GetFFProbePath();

            CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));

            try
            {
                await Cli.Wrap(binPath!)
                .WithArguments(args)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                        .ExecuteAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Out.WriteLine($"{DateTime.Now} | {WarningMessage.StreamDurationTimeout}");
                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine($"{DateTime.Now} | {ex}");
                return TimeSpan.Zero;
            }

            string? stdOut = stdOutBuffer.ToString();

            if (string.IsNullOrEmpty(stdOut))
            {

                return TimeSpan.Zero;
            }


            return TimeSpan.Parse(stdOut);
        }

        private static async Task<TimeSpan> GetStreamDurationFromFile(string filePath)
        {
            StringBuilder StdOutBuffer = new();

            string? binPath = Helper.GetFFProbePath();

            string arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -sexagesimal \"{filePath}\"";

            CancellationTokenSource ffProbeCTS = new(TimeSpan.FromSeconds(5));
            CancellationToken ffProbeToken = ffProbeCTS.Token;

            try
            {
                await Cli.Wrap(binPath!)
                         .WithArguments(arguments)
                         .WithValidation(CommandResultValidation.None)
                             .WithStandardOutputPipe(PipeTarget.ToStringBuilder(StdOutBuffer))
                                 //.WithStandardErrorPipe(PipeTarget.ToStringBuilder(StdErrBuffer))
                                 .ExecuteAsync(ffProbeToken);
            }
            catch (OperationCanceledException)
            {
                return TimeSpan.Zero;
            }
            finally
            {
                ffProbeCTS.Dispose();
            }

            string output = StdOutBuffer.ToString().TrimEnd();

            StdOutBuffer.Clear();

            if (string.IsNullOrEmpty(output) || output == "N/A")
                return TimeSpan.Zero;

            return TimeSpan.Parse(output);
        }

        public static DownloadModel? GetDownload()
        {
            return Download;
        }

        public static ConverterState GetConverterState()
        {
            return ConverterState;
        }

        public static void Abort(bool isSkip = false)
        {
            AbortIsSkip = isSkip;

            if (CTS is not null && !CTS.Token.IsCancellationRequested)
            {
                CTS.Cancel();
            }
        }
    }
}
