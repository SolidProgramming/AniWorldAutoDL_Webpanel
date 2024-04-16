using Quartz;

namespace AniWorldAutoDL_Webpanel.Interfaces
{
    public interface IQuartzService
    {
        Task Init();
        Task CreateJob(int intervalInMinutes);
        Task StartJob();
    }
}
