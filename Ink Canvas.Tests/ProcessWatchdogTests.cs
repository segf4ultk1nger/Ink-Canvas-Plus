using InkCanvasPlus;
using InkCanvasPlus.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class ProcessWatchdogTests
    {
        [TestMethod]
        public void AutomationDefaults_AutoKillFlagsRemainOn()
        {
            var automation = new Automation();
            Assert.IsTrue(automation.IsAutoKillPptService);
            Assert.IsTrue(automation.IsAutoKillEasiNote);
        }

        [TestMethod]
        public void Tick_WhenPptServiceRunning_RunsTaskkillWithPptService()
        {
            string lastArg = null;
            var floatBall = 0;
            using (var watchdog = Create(
                killPpt: true,
                killEasi: false,
                running: "PPTService",
                runTaskKill: arg => lastArg = arg,
                killFloatBall: () => floatBall++))
            {
                watchdog.Tick();
            }

            Assert.AreEqual("/F /IM PPTService.exe", lastArg);
            Assert.AreEqual(0, floatBall);
        }

        [TestMethod]
        public void Tick_WhenSeewoAssistantRunning_IncludesGuardInTaskkill()
        {
            string lastArg = null;
            using (var watchdog = Create(
                killPpt: true,
                killEasi: false,
                running: "SeewoIwbAssistant",
                runTaskKill: arg => lastArg = arg))
            {
                watchdog.Tick();
            }

            Assert.AreEqual("/F /IM SeewoIwbAssistant.exe /IM Sia.Guard.exe", lastArg);
        }

        [TestMethod]
        public void Tick_WhenBothCompanionProcessesRunning_ConcatenatesTaskkillArgs()
        {
            string lastArg = null;
            using (var watchdog = Create(
                killPpt: true,
                killEasi: false,
                runningNames: new HashSet<string> { "PPTService", "SeewoIwbAssistant" },
                runTaskKill: arg => lastArg = arg))
            {
                watchdog.Tick();
            }

            Assert.AreEqual("/F /IM PPTService.exe /IM SeewoIwbAssistant.exe /IM Sia.Guard.exe", lastArg);
        }

        [TestMethod]
        public void Tick_WhenKillPptFlagOff_DoesNotTaskkillEvenIfProcessExists()
        {
            string lastArg = null;
            using (var watchdog = Create(
                killPpt: false,
                killEasi: false,
                running: "PPTService",
                runTaskKill: arg => lastArg = arg))
            {
                watchdog.Tick();
            }

            Assert.IsNull(lastArg);
        }

        [TestMethod]
        public void Tick_WhenEasiNoteRunningAndFlagOn_CallsFloatBallKiller()
        {
            var floatBall = 0;
            string lastArg = null;
            using (var watchdog = Create(
                killPpt: false,
                killEasi: true,
                running: "EasiNote",
                runTaskKill: arg => lastArg = arg,
                killFloatBall: () => floatBall++))
            {
                watchdog.Tick();
            }

            Assert.AreEqual(1, floatBall);
            Assert.IsNull(lastArg);
        }

        [TestMethod]
        public void Tick_WhenEasiNoteRunningAndFlagOff_DoesNotCallFloatBallKiller()
        {
            var floatBall = 0;
            using (var watchdog = Create(
                killPpt: false,
                killEasi: false,
                running: "EasiNote",
                killFloatBall: () => floatBall++))
            {
                watchdog.Tick();
            }

            Assert.AreEqual(0, floatBall);
        }

        private static ProcessWatchdog Create(
            bool killPpt,
            bool killEasi,
            string running = null,
            HashSet<string> runningNames = null,
            Action<string> runTaskKill = null,
            Action killFloatBall = null)
        {
            var names = runningNames ?? new HashSet<string>();
            if (running != null) names.Add(running);

            return new ProcessWatchdog(
                isAutoKillPptService: () => killPpt,
                isAutoKillEasiNote: () => killEasi,
                countProcessesByName: name => names.Contains(name) ? 1 : 0,
                runTaskKill: runTaskKill ?? (_ => { }),
                killEasiNoteFloatBall: killFloatBall ?? (() => { }));
        }
    }
}
