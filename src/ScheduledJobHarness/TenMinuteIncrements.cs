﻿using System;
using FubuTransportation.ScheduledJobs.Execution;
using FubuTransportation.ScheduledJobs.Persistence;

namespace ScheduledJobHarness
{
    public class TenMinuteIncrements : IScheduleRule
    {
        public DateTimeOffset ScheduleNextTime(DateTimeOffset currentTime, JobExecutionRecord lastExecution)
        {
            var hour = new DateTimeOffset(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, 0, 0,
                currentTime.Offset);



            while (hour < currentTime)
            {
                hour = hour.AddMinutes(10);
            }

            return hour;
        }
    }
}