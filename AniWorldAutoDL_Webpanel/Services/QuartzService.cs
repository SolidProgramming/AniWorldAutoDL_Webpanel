using Quartz;

namespace AniWorldAutoDL_Webpanel.Services
{
    public class QuartzService(ISchedulerFactory schedulerFactory) : IQuartzService
    {
        private CancellationTokenSource CancellationTokenSource = new();
        public CancellationToken CancellationToken { get; set; }

        private IScheduler? Scheduler;

        private string? JobName;
        private JobKey? JobKey;

        public async Task Init()
        {
            CancellationToken = CancellationTokenSource.Token;
            Scheduler = await schedulerFactory.GetScheduler(CancellationToken);

            JobName = typeof(CronJob).Name;
            JobKey = new(JobName);
        }

        public async Task CreateJob(int intervalInMinutes)
        {
            IJobDetail? job = JobBuilder.Create<CronJob>()
                       .WithIdentity(JobKey)
                       .Build();

            DateTimeOffset startTime = new DateTimeOffset(DateTime.Now.ToLocalTime())
                                                 .AddSeconds(10);

            ITrigger trigger = TriggerBuilder.Create()
                .ForJob(JobKey)
                .WithIdentity(JobName + "-trigger")
                .WithSimpleSchedule(_ =>
                    _.WithIntervalInMinutes(intervalInMinutes)
                    .RepeatForever())
                .StartAt(startTime)
                .Build();

            CronJob.NextRun = startTime.DateTime;
            CronJob.Interval = intervalInMinutes;

            await Scheduler.ScheduleJob(job, trigger, CancellationToken);
        }

        public async Task StartJob()
        {
            await Scheduler.TriggerJob(JobKey, CancellationToken);
        }
    }
}
