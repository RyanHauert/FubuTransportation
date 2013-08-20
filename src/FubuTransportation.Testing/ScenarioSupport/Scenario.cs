﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bottles.Services.Messaging.Tracking;
using FubuCore.Logging;
using FubuTransportation.Configuration;
using FubuTransportation.InMemory;
using System.Linq;

namespace FubuTransportation.Testing.ScenarioSupport
{
    public class Scenario
    {
        public readonly NodeConfiguration Website1 = new NodeConfiguration(x => x.Website1);
        public readonly NodeConfiguration Website2 = new NodeConfiguration(x => x.Website2);
        public readonly NodeConfiguration Website3 = new NodeConfiguration(x => x.Website3);
        public readonly NodeConfiguration Website4 = new NodeConfiguration(x => x.Website4);
        
        public readonly NodeConfiguration Service1 = new NodeConfiguration(x => x.Service1);
        public readonly NodeConfiguration Service2 = new NodeConfiguration(x => x.Service2);
        public readonly NodeConfiguration Service3 = new NodeConfiguration(x => x.Service3);
        public readonly NodeConfiguration Service4 = new NodeConfiguration(x => x.Service4);

        private readonly IEnumerable<NodeConfiguration> _configurations;
        private readonly IList<IScenarioStep> _steps = new List<IScenarioStep>(); 

        public Scenario()
        {
            Title = GetType().Name.Replace("_", " ");

            _configurations = new NodeConfiguration[]
            {
                Website1,
                Website2,
                Website3,
                Website4,
                Service1,
                Service2,
                Service3,
                Service4
            };

            _configurations.Each(x => x.Parent = this);
        }

        public string Title { get; set; }

        internal void Execute(IScenarioWriter writer)
        {
            FubuTransport.SetupForInMemoryTesting();

            InMemoryQueueManager.ClearAll();
            TestMessageRecorder.Clear();
            MessageHistory.ClearAll();

            _configurations.Each(x => x.SpinUp());

            writer.WriteTitle(Title);

            using (writer.Indent())
            {
                writeArrangement(writer);

                writer.WriteLine("Actions");

                using (writer.Indent())
                {
                    _steps.Each(x => {
                        x.PreviewAct(writer);
                        try
                        {
                            x.Act(writer);
                        }
                        catch (Exception e)
                        {
                            writer.Exception(e);
                        }
                    });

                }

                Wait.Until(() => {
                    return !MessageHistory.Outstanding().Any();
                }, timeoutInMilliseconds:5000);

                writer.BlankLine();

                writer.WriteLine("Assertions");

                using (writer.Indent())
                {
                    _steps.Each(x => {
                        x.PreviewAssert(writer);
                        x.Assert(writer);
                    });
                }


                writer.BlankLine();


                if (TestMessageRecorder.AllProcessed.Any())
                {
                    writer.WriteLine("Messages Received");
                    TestMessageRecorder.AllProcessed.Each(x => {
                        writer.Bullet("{0} received by {1}", x.Message.GetType().Name, x.Message.Source);
                    });
                }
                else
                {
                    writer.WriteLine("No messages were received!");
                }

                var unexpectedMessages = TestMessageRecorder.AllProcessed.Where(x => !_steps.Any(step => step.MatchesSentMessage(x.Message)))
                                   .ToArray();

                if (unexpectedMessages.Any())
                {
                    writer.BlankLine();
                    writer.WriteLine("Found unexpected messages");
                    unexpectedMessages.Each(x => writer.Failure(x.ToString()));
                }
            }
        }

        internal void Preview(IScenarioWriter writer)
        {
            _configurations.Each(x => x.SpinUp());

            writer.WriteTitle(Title);

            using (writer.Indent())
            {
                writeArrangement(writer);

                writer.WriteLine("Actions");

                using (writer.Indent())
                {
                    _steps.Each(x => x.PreviewAct(writer));
                }

                writer.BlankLine();

                writer.WriteLine("Assertions");

                using (writer.Indent())
                {
                    _steps.Each(x => x.PreviewAssert(writer));
                }
            }
        }

        private void writeArrangement(IScenarioWriter writer)
        {
            _configurations.Each(x => {
                x.Describe(writer);
            });

            writer.BlankLine();
        }

        internal void AddStep(IScenarioStep step)
        {
            _steps.Add(step);
        }
    }

    public class ScenarioLogListener : ILogListener
    {
        private readonly string _nodeName;

        public ScenarioLogListener(string nodeName)
        {
            _nodeName = nodeName;
        }


        public bool ListensFor(Type type)
        {
            return true;
        }

        private void write(object message)
        {
            System.Diagnostics.Debug.WriteLine("{0}: {1}", _nodeName, message);
        }

        public void DebugMessage(object message)
        {
            write(message);
        }

        public void InfoMessage(object message)
        {
            write(message);
        }

        public void Debug(string message)
        {
            write(message);
        }

        public void Info(string message)
        {
            write(message);
        }

        public void Error(string message, Exception ex)
        {
            write(message);
            write(ex);
        }

        public void Error(object correlationId, string message, Exception ex)
        {
            write(correlationId);
            write(message);
            write(ex);
        }

        public bool IsDebugEnabled { get { return true; } }
        public bool IsInfoEnabled { get { return true; } }
    }
}

