using InkCanvasPlus.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class PowerPointSessionTests
    {
        [TestMethod]
        public void Detach_NeverAttached_DoesNotThrow()
        {
            using (var session = new PowerPointSession(
                dispatcher: null,
                hasWpsProcess: () => false,
                isWpsSupported: () => true,
                getActiveObject: () => { throw new InvalidOperationException("should not attach"); }))
            {
                session.Detach();
                session.Detach();
                Assert.IsFalse(session.IsAttached);
                Assert.AreEqual(0, session.SubscribeCount);
            }
        }

        [TestMethod]
        public void Dispose_NeverAttached_DoesNotThrow()
        {
            var session = new PowerPointSession(
                dispatcher: null,
                hasWpsProcess: () => false,
                isWpsSupported: () => true,
                getActiveObject: () => { throw new InvalidOperationException("should not attach"); });
            session.Dispose();
            session.Dispose();
            Assert.IsFalse(session.TryAttach());
        }

        [TestMethod]
        public void ShouldSkipBecauseWps_WhenWpsRunningAndUnsupported_DoesNotCallGetActiveObject()
        {
            var getActiveCalls = 0;
            using (var session = new PowerPointSession(
                dispatcher: null,
                hasWpsProcess: () => true,
                isWpsSupported: () => false,
                getActiveObject: () =>
                {
                    getActiveCalls++;
                    return new object();
                }))
            {
                Assert.IsTrue(session.ShouldSkipBecauseWps());
                if (!session.ShouldSkipBecauseWps())
                {
                    session.TryAttach();
                }

                Assert.AreEqual(0, getActiveCalls);
                Assert.IsFalse(session.IsAttached);
                Assert.AreEqual(0, session.SubscribeCount);
            }
        }

        [TestMethod]
        public void TryAttach_IgnoresWpsSkip_SoButtonPathStillAttaches()
        {
            var getActiveCalls = 0;
            using (var session = new PowerPointSession(
                dispatcher: null,
                hasWpsProcess: () => true,
                isWpsSupported: () => false,
                getActiveObject: () =>
                {
                    getActiveCalls++;
                    return new object();
                }))
            {
                Assert.IsTrue(session.ShouldSkipBecauseWps());
                Assert.IsTrue(session.TryAttach());
                Assert.AreEqual(1, getActiveCalls);
                Assert.IsTrue(session.IsAttached);
            }
        }

        [TestMethod]
        public void TryAttach_SecondCall_DoesNotSubscribeTwice()
        {
            var getActiveCalls = 0;
            using (var session = new PowerPointSession(
                dispatcher: null,
                hasWpsProcess: () => false,
                isWpsSupported: () => true,
                getActiveObject: () =>
                {
                    getActiveCalls++;
                    return new object();
                }))
            {
                Assert.IsTrue(session.TryAttach());
                Assert.IsTrue(session.TryAttach());
                Assert.AreEqual(1, getActiveCalls);
                Assert.AreEqual(1, session.SubscribeCount);
            }
        }

        [TestMethod]
        public void TryAttach_WhenGetActiveObjectFails_ReturnsFalse()
        {
            using (var session = new PowerPointSession(
                dispatcher: null,
                hasWpsProcess: () => false,
                isWpsSupported: () => true,
                getActiveObject: () => { throw new COMMissingException(); }))
            {
                Assert.IsFalse(session.TryAttach());
                Assert.IsFalse(session.IsAttached);
                Assert.AreEqual(0, session.SubscribeCount);
            }
        }

        private sealed class COMMissingException : Exception
        {
        }
    }
}
