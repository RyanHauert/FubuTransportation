﻿using FubuTransportation.Configuration;
using FubuTransportation.Polling;

namespace FubuTransportation.ScheduledJobs
{
    public class ScheduledJobExpression
    {
        private readonly ScheduledJobGraph _graph;
        private readonly FubuTransportRegistry _parent;

        public ScheduledJobExpression(ScheduledJobGraph graph)
        {
            _graph = graph;
        }

        public ScheduleExpression<TJob> RunJob<TJob>() where TJob : IJob
        {
            return new ScheduleExpression<TJob>(this);
        }

        public class ScheduleExpression<TJob> where TJob : IJob
        {
            private readonly ScheduledJobExpression _parent;

            public ScheduleExpression(ScheduledJobExpression parent)
            {
                _parent = parent;
            }

            public ScheduledJobExpression ScheduledBy<TScheduler>() where TScheduler : IScheduleRule, new()
            {
                return ScheduledBy(new TScheduler());
            }

            public ScheduledJobExpression ScheduledBy(IScheduleRule rule)
            {
                var definition = new ScheduledJob(typeof (TJob), rule);

                _parent._graph.Jobs.Add(definition);

                return _parent;
            }
        }
    }
}