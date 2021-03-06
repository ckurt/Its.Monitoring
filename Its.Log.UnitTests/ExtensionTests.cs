// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Linq;
using Its.Log.Instrumentation.Extensions;
using Moq;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace Its.Log.Instrumentation.UnitTests
{
    /// <summary>
    /// Unit tests for ExtensionTests
    /// </summary>
    [TestFixture]
    public class ExtensionTests
    {
        [SetUp]
        public void SetUp()
        {
            Extension.EnableAll();
        }

        [TearDown]
        public void CleanUp()
        {
            Log.UnsubscribeAllFromEntryPosted();
        }

        public enum EntryType
        {
            Information,
            Error
        }

        [Test]
        public virtual void EnableAll_enables_extensions_that_were_disabled_individually()
        {
            Extension<Params>.Disable();
            Assert.IsFalse(Extension<Params>.IsEnabled);

            Extension.EnableAll();

            Assert.IsTrue(Extension<Params>.IsEnabled);
        }

        [Test]
        public virtual void Exceptions_thrown_in_extension_anonymous_delegates_are_published_to_error_events()
        {
            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(o => o.OnNext(It.Is<LogEntry>(e => e.Subject is DivideByZeroException)));

            using (observer.Object.SubscribeToLogInternalErrors())
            {
                Log
                    .With<EventLogInfo>(el => el.EventId = el.EventId/(el.EventId - el.EventId))
                    .Write("here i am");
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Exceptions_thrown_in_extension_anonymous_delegates_do_not_bubble_up()
        {
            Log
                .With<EventLogInfo>(el => el.EventId = el.EventId/(el.EventId - el.EventId))
                .Write("here i am");
        }

        [Test]
        public virtual void Extensions_can_be_disabled_globally_separately()
        {
            Extension<EventLogInfo>.Disable();
            Extension<Params>.Enable();

            Assert.IsTrue(Extension<Params>.IsEnabled);
            Assert.IsFalse(Extension<EventLogInfo>.IsEnabled);
        }

        [NUnit.Framework.Ignore("Not supported")]
        [Test]
        public virtual void When_same_extension_type_is_added_more_than_once_all_instances_are_logged()
        {
            var observer = new Mock<IObserver<LogEntry>>();
            observer
                .Setup(
                    o => o.OnNext(
                        It.Is<LogEntry>(
                            e => e.ToLogString().Contains("11111") &&
                                 e.ToLogString().Contains("22222"))));

            using (observer.Object.SubscribeToLogEvents())
            {
                Log
                    .With<EventLogInfo>(el => el.EventId = 11111)
                    .With<EventLogInfo>(el => el.EventId = 22222)
                    .Write("here i am");
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Enter_With_with_overload_having_no_initialization_action_writes_extension_on_enter_and_exit()
        {
            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(
                o =>
                o.OnNext(
                    It.Is<LogEntry>(e =>
                                    e.EventType == TraceEventType.Start &&
                                    e.HasExtension<FooExtension>())));
            observer.Setup(
                o =>
                o.OnNext(
                    It.Is<LogEntry>(e =>
                                    e.EventType == TraceEventType.Stop &&
                                    e.HasExtension<FooExtension>())));

            using (observer.Object.SubscribeToLogEvents())
            {
                using (Log.With<FooExtension>().Enter(() => { }))
                {
                }
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Enter_and_Dispose_magic_barbell_overload_with_chained_extensions_writes_extension_info_twice()
        {
            var eventId = 424242;
            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(
                o =>
                o.OnNext(
                    It.Is<LogEntry>(e =>
                                    e.EventType == TraceEventType.Start &&
                                    e.GetExtension<Counter>().Count == 1 &&
                                    e.GetExtension<EventLogInfo>().EventId == eventId)));
            observer.Setup(
                o =>
                o.OnNext(
                    It.Is<LogEntry>(e =>
                                    e.EventType == TraceEventType.Stop &&
                                    e.GetExtension<Counter>().Count == 1 &&
                                    e.GetExtension<EventLogInfo>().EventId == eventId)));

            using (Log.Events().LogToConsole())
            using (observer.Object.SubscribeToLogEvents())
            {
                using (Log
                    .With<Counter>(c => c.Increment())
                    .With<EventLogInfo>(ext => { ext.EventId = eventId; })
                    .Enter(() => { }))
                {
                }
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Enter_and_Dispose_params_overload_with_chained_extensions_writes_extension_info_twice()
        {
            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(
                o =>
                o.OnNext(
                    It.Is<LogEntry>(e =>
                                    e.EventType == TraceEventType.Start &&
                                    e.GetExtension<Counter>().Count == 1 &&
                                    e.GetExtension<EventLogInfo>().EventId == 123)));
            observer.Setup(
                o =>
                o.OnNext(
                    It.Is<LogEntry>(e =>
                                    e.EventType == TraceEventType.Stop &&
                                    e.GetExtension<Counter>().Count == 1 &&
                                    e.GetExtension<EventLogInfo>().EventId == 123)));

            using (observer.Object.SubscribeToLogEvents())
            {
                using (Log
                    .With<Counter>(c => c.Increment())
                    .With<EventLogInfo>(ext => ext.EventId = 123)
                    .Enter(() => new { Foo = "foo" }))
                {
                }
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Enter_and_Dispose_magic_barbell_overload_with_single_extension_writes_extension_info_twice()
        {
            var i = 3246362;

            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(o => o.OnNext(It.Is<LogEntry>(e =>
                                                         e.EventType == TraceEventType.Start &&
                                                         e.GetExtension<EventLogInfo>().EventId == i)));
            observer.Setup(o => o.OnNext(It.Is<LogEntry>(e =>
                                                         e.EventType == TraceEventType.Stop &&
                                                         e.GetExtension<EventLogInfo>().EventId == i)));

            using (TestHelper.LogToConsole())
            using (observer.Object.SubscribeToLogEvents())
            {
                using (Log
                    .With<EventLogInfo>(ext => ext.EventId = i)
                    .Enter(() => { }))
                {
                }
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Enter_and_Dispose_params_overload_with_single_extension_writes_extension_info_twice()
        {
            var i = 3246362;

            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(o => o.OnNext(It.Is<LogEntry>(e =>
                                                         e.EventType == TraceEventType.Start &&
                                                         e.GetExtension<EventLogInfo>().EventId == i)));
            observer.Setup(o => o.OnNext(It.Is<LogEntry>(e =>
                                                         e.EventType == TraceEventType.Stop &&
                                                         e.GetExtension<EventLogInfo>().EventId == i)));

            using (observer.Object.SubscribeToLogEvents())
            {
                using (Log
                    .With<EventLogInfo>(ext => ext.EventId = i)
                    .Enter(() => new { Foo = "foo" }))
                {
                }
            }

            observer.VerifyAll();
        }

        [Test]
        public virtual void Write_with_single_extension_writes_extension_info()
        {
            var i = 3246362;

            var observer = new Mock<IObserver<LogEntry>>();
            observer.Setup(
                o => o.OnNext(
                    It.Is<LogEntry>(e => e.GetExtension<EventLogInfo>().EventId == i)));

            using (observer.Object.SubscribeToLogEvents())
            using (TestHelper.OnEntryPosted(Console.WriteLine))
            {
                Log
                    .With<EventLogInfo>(ext => ext.EventId = i)
                    .Write("SingleExtension");
            }

            observer.VerifyAll();
        }

        [Test]
        public void Params_usage_of_formatter_for_ToString_does_not_cause_stack_overflow()
        {
            var count = 0;
            for (int i = 1; i <= 20; i++)
            {
                Formatter.RecursionLimit = i;
                using (TestHelper.LogToConsole())
                using (Log.Events().Subscribe(e => count++))
                {
                    Log.WithParams(() => new { Formatter.RecursionLimit }).Write("testing...");
                }
            }

            Assert.That(count, Is.EqualTo(20));
        }
    }

    public class FooExtension
    {
        public string Foo = "bar";
    }
}