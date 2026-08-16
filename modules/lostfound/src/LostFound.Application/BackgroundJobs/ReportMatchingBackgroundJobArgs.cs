using System;

namespace LostFound.BackgroundJobs
{
    [Serializable]
    public class ReportMatchingBackgroundJobArgs
    {
        public Guid ReportId { get; set; }
    }
}
