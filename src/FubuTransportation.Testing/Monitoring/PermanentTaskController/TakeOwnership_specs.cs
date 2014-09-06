﻿using System;
using System.Threading.Tasks;
using FubuTestingSupport;
using FubuTransportation.Monitoring;
using NUnit.Framework;

namespace FubuTransportation.Testing.Monitoring.PermanentTaskController
{
    [TestFixture]
    public class when_taking_ownership_successfully : PersistentTaskControllerContext
    {
        private const string theSubjectUriString = "good://1";
        private Task<OwnershipStatus> theTask;

        protected override void theContextIs()
        {
            Task(theSubjectUriString).IsFullyFunctional();
            Task(theSubjectUriString).Timesout = false;

            theTask = theController.TakeOwnership(theSubjectUriString.ToUri());

            theTask.Wait();
        }

        [Test]
        public void should_return_OwnershipActivated()
        {
            theTask.Result.ShouldEqual(OwnershipStatus.OwnershipActivated);
        }

        [Test]
        public void activates_the_task()
        {
            Task(theSubjectUriString).IsActive.ShouldBeTrue();
        }

        [Test]
        public void logs_the_activation()
        {
            LoggedMessageForSubject<TookOwnershipOfPersistentTask>(theSubjectUriString);
        }

        
        [Test]
        public void persists_the_new_ownership()
        {
            theCurrentNode.OwnedTasks.ShouldContain(theSubjectUriString.ToUri());
        }
    }

    [TestFixture]
    public class when_taking_ownership_unsuccessfully : PersistentTaskControllerContext
    {
        private const string theSubjectUriString = "bad://1";
        private Task<OwnershipStatus> theTask;

        protected override void theContextIs()
        {
            Task(theSubjectUriString).ActivationException = new DivideByZeroException();

            theTask = theController.TakeOwnership(theSubjectUriString.ToUri());

            theTask.Wait();
        }

        [Test]
        public void should_return_Exception()
        {
            theTask.Result.ShouldEqual(OwnershipStatus.Exception);
        }


        [Test]
        public void logs_the_activation_failure()
        {
            LoggedMessageForSubject<TaskActivationFailure>(theSubjectUriString);
        }

        [Test]
        public void does_not_persist_the_new_ownership()
        {
            theCurrentNode.OwnedTasks.ShouldNotContain(theSubjectUriString.ToUri());
        }
    }




    [TestFixture]
    public class when_trying_to_take_ownership_of_an_already_active_task : PersistentTaskControllerContext
    {
        private const string theSubjectUriString = "good://1";
        private Task<OwnershipStatus> theTask;

        protected override void theContextIs()
        {
            Task(theSubjectUriString).IsFullyFunctionalAndActive();

            theTask = theController.TakeOwnership(theSubjectUriString.ToUri());

            theTask.Wait();
        }

        [Test]
        public void should_return_OwnershipActivated()
        {
            theTask.Result.ShouldEqual(OwnershipStatus.AlreadyOwned);
        }
    }

    [TestFixture]
    public class when_trying_to_take_ownership_of_an_unknown_task : PersistentTaskControllerContext
    {
        [Test]
        public void should_return_Unknown()
        {
            var task = theController.TakeOwnership("unknown://1".ToUri());
            task.Wait();
            task.Result.ShouldEqual(OwnershipStatus.UnknownSubject);

        }
    }
}