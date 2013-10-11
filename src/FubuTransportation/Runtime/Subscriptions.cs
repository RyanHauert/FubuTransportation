﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using FubuCore;
using FubuCore.DependencyAnalysis;
using FubuTransportation.Configuration;
using FubuTransportation.Runtime.Invocation;
using FubuTransportation.Scheduling;

namespace FubuTransportation.Runtime
{
    public class Subscriptions : ISubscriptions, IDisposable
    {
        private readonly ChannelGraph _graph;
        private readonly IEnumerable<ITransport> _transports;
        private readonly Lazy<IHandlerPipeline> _pipeline;

        public Subscriptions(ChannelGraph graph, Func<IHandlerPipeline> invoker, IEnumerable<ITransport> transports)
        {
            _graph = graph;
            _transports = transports;
            _pipeline = new Lazy<IHandlerPipeline>(invoker);
        }

        public IEnumerable<ChannelNode> FindChannels(Envelope envelope)
        {
            if (envelope.Destination != null)
            {
                var destination = findDestination(envelope);

                return new ChannelNode[]{destination};
            }

            // TODO -- gets a LOT more sophisticated later
            var inputType = envelope.Message.GetType();
            return _graph.Where(c => c.Rules.Any(x => x.Matches(inputType)));
        }

        private ChannelNode findDestination(Envelope envelope)
        {
            var destination = _graph.FirstOrDefault(x => x.Uri == envelope.Destination);
            if (destination == null)
            {
                var transport = _transports.FirstOrDefault(x => x.Protocol == envelope.Destination.Scheme);
                if (transport == null)
                {
                    throw new UnknownChannelException(envelope.Destination);
                }

                var node = new ChannelNode {Uri = envelope.Destination, Key = envelope.Destination.ToString()};
                node.Channel = transport.BuildDestinationChannel(node.Uri);

                return node;
            }

            return destination;
        }

        public void Dispose()
        {
            _graph.Each(x =>
            {
                var shutdownVisitor = new ShutdownChannelNodeVisitor();
                shutdownVisitor.Visit(x);
            });
        }

        public void Start()
        {
            _transports.Each(x => x.OpenChannels(_graph));

            var missingChannels = _graph.Where(x => x.Channel == null);
            if (missingChannels.Any())
            {
                throw new InvalidOrMissingTransportException(_transports, missingChannels);
            }

            _graph.StartReceiving(_pipeline.Value);
        }

        public ChannelNode ReplyNodeFor(ChannelNode destination)
        {
            return _graph.FirstOrDefault(x => x.Protocol() == destination.Protocol() && x.ForReplies);
        }
    }

    [Serializable]
    public class InvalidOrMissingTransportException : Exception
    {
        public static string ToMessage(IEnumerable<ITransport> transports, IEnumerable<ChannelNode> nodes)
        {
            return "Missing channel Uri configuration or unknown transport types\nAvailable transports are " + transports.Select(x => x.ToString()).Join(", ") + " and the invalid nodes are \n" + nodes.Select(x => {
                return "Node '{0}'@{1}; ".ToFormat(x.Key, x.Uri);
            }).Join("\n");
        }

        public InvalidOrMissingTransportException(IEnumerable<ITransport> transports, IEnumerable<ChannelNode> nodes)
            : base(ToMessage(transports, nodes))
        {
            
        }

        protected InvalidOrMissingTransportException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}