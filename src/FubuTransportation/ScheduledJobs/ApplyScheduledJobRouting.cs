﻿using System;
using System.Collections.Generic;
using System.Linq;
using FubuCore;
using FubuMVC.Core.Registration;
using FubuTransportation.Configuration;

namespace FubuTransportation.ScheduledJobs
{
    // Tested with integration tests only
    public class ApplyScheduledJobRouting : IConfigurationAction
    {
        public void Configure(BehaviorGraph graph)
        {
            var jobs = graph.Settings.Get<ScheduledJobGraph>();
            var channels = graph.Settings.Get<ChannelGraph>();

            if (jobs.Jobs.Any(x => x.Channel == null) && jobs.DefaultChannel == null)
            {
                var missing = jobs.Jobs.Where(x => x.Channel == null);
                var message =
                    "No channel configured for jobs {0} and no default Scheduled job channel configured".ToFormat(
                        missing.Select(x => x.JobType.GetFullName()).Join(", "));

                throw new InvalidOperationException(message);
            }

            jobs.Jobs.Each(job => {
                var accessor = job.Channel ?? jobs.DefaultChannel;
                var channel = channels.ChannelFor(accessor);
                if (channel == null)
                {
                    throw new InvalidOperationException("Nonexistent Channel '{0}' configured for Scheduled job {1}".ToFormat(accessor, job.JobType.GetFullName()));
                }

                channel.Rules.Add(job.ToRoutingRule());
            });
        }
    }
}