﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FubuCore;
using FubuCore.Logging;
using FubuCore.Util;
using FubuTransportation.Configuration;
using FubuTransportation.Subscriptions;

namespace FubuTransportation.Monitoring
{
    public interface IPersistentTasks
    {
        IPersistentTask FindTask(Uri subject);
        IPersistentTaskAgent FindAgent(Uri subject);
        IEnumerable<Uri> PersistentSubjects { get; }
        string NodeId { get; }
    }

    public interface IPersistentTaskController
    {
        Task<HealthStatus> CheckStatus(Uri subject);
        Task<bool> Deactivate(Uri subject);
        Task EnsureTasksHaveOwnership();
        Task<OwnershipStatus> TakeOwnership(Uri subject);
        Task<TaskHealthResponse> CheckStatusOfOwnedTasks();
        IEnumerable<Uri> ActiveTasks();
    }

    public class PersistentTaskController : ITransportPeer, IPersistentTasks, IPersistentTaskController
    {
        private readonly ChannelGraph _graph;
        private readonly ILogger _logger;
        private readonly ITransportPeerFactory _factory;
        private readonly HealthMonitoringSettings _settings;
        private readonly ISubscriptionRepository _repository;

        private readonly ConcurrentCache<string, IPersistentTaskSource> _sources
            = new ConcurrentCache<string, IPersistentTaskSource>();

        private readonly ConcurrentCache<Uri, PersistentTaskAgent> _agents =
            new ConcurrentCache<Uri, PersistentTaskAgent>();


        private readonly Uri[] _permanentTasks;


        public PersistentTaskController(ChannelGraph graph, ILogger logger, ITransportPeerFactory factory, IEnumerable<IPersistentTaskSource> sources, HealthMonitoringSettings settings, ISubscriptionRepository repository)
        {
            _graph = graph;
            _logger = logger;
            _factory = factory;
            _settings = settings;
            _repository = repository;
            sources.Each(x => _sources[x.Protocol] = x);

            _agents.OnMissing = uri => {
                var persistentTask = FindTask(uri);
                if (persistentTask == null) return null;

                return new PersistentTaskAgent(persistentTask, _settings, _logger, _repository);
            };

            _permanentTasks = sources.SelectMany(x => x.PermanentTasks()).ToArray();
        }

        public Task<HealthStatus> CheckStatus(Uri subject)
        {
            var agent = _agents[subject];

            if (agent == null)
            {
                return HealthStatus.Unknown.ToCompletionTask();
            }

            return checkStatus(agent);
        }

        private Task<HealthStatus> checkStatus(PersistentTaskAgent agent)
        {
            return agent.IsActive ? agent.AssertAvailable() : HealthStatus.Inactive.ToCompletionTask();
        }


        public IPersistentTask FindTask(Uri subject)
        {
            if (!_sources.Has(subject.Scheme)) return null;

            var source = _sources[subject.Scheme];
            if (source == null) return null;

            return source.CreateTask(subject);
        }

        public IPersistentTaskAgent FindAgent(Uri subject)
        {
            return _agents[subject];
        }

        IEnumerable<Uri> IPersistentTasks.PersistentSubjects
        {
            get { return _permanentTasks; }
        }

        public Task<bool> Deactivate(Uri subject)
        {
            var agent = _agents[subject];
            if (agent == null)
            {
                _logger.Info("Task '{0}' is not recognized by this node".ToFormat(subject));

                return false.ToCompletionTask();
            }

            return agent.Deactivate();
        }



        public Task EnsureTasksHaveOwnership()
        {
            using (var router = new HealthAndAssignmentRouter(_logger,this, allPeers().ToArray()))
            {
                return router.EnsureAllTasksAreAssignedAndRunning();
            }
        }


        private IEnumerable<ITransportPeer> allPeers()
        {
            yield return this;
            foreach (var peer in _factory.BuildPeers())
            {
                yield return peer;
            }
        } 

        public Task<OwnershipStatus> TakeOwnership(Uri subject)
        {
            _logger.InfoMessage(() => new TryingToAssignOwnership(subject, NodeId));

            var agent = _agents[subject];
            if (agent == null)
            {
                
                return OwnershipStatus.UnknownSubject.ToCompletionTask();
            }

            if (agent.IsActive)
            {
                return OwnershipStatus.AlreadyOwned.ToCompletionTask();
            }


            return agent.Activate();
        }

        public Task<TaskHealthResponse> CheckStatusOfOwnedTasks()
        {
            var subjects = CurrentlyOwnedSubjects();

            if (!subjects.Any())
            {
                return TaskHealthResponse.Empty().ToCompletionTask();
            }

            var checks = subjects
                .Select(subject => CheckStatus(subject).ContinueWith(t => new PersistentTaskStatus(subject, t.Result)))
                .ToArray();

            return Task.Factory.ContinueWhenAll(checks, tasks => new TaskHealthResponse
            {
                Tasks = tasks.Select(x => x.Result).ToArray()
            });
        }

        public IEnumerable<Uri> ActiveTasks()
        {
            return _agents.Where(x => x.IsActive).Select(x => x.Subject).ToArray();
        }

        public IEnumerable<Uri> CurrentlyOwnedSubjects()
        {
            var activeTasks = _agents.Where(x => x.IsActive).Select(x => x.Subject);
            return
                _repository.FindLocal().OwnedTasks.Union(activeTasks).ToArray();
        }

        public string NodeId
        {
            get { return _graph.NodeId; }
        }

        string ITransportPeer.MachineName
        {
            get { return Environment.MachineName; }
        }

        // TODO -- think this should be explicitly set later
        public Uri ControlChannel
        {
            get
            {
                return _graph.ReplyUriList().FirstOrDefault();
            }
        }

    }


}