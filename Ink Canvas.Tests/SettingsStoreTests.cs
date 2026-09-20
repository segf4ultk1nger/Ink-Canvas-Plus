using InkCanvasPlus.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class SettingsStoreTests
    {
        private string _tempDir;

        [TestInitialize]
        public void TestInitialize()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "icp-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
            }
        }

        [TestMethod]
        public void SaveThenLoad_RoundtripsSelectedProperties()
        {
            using (var store = new SettingsStore(_tempDir))
            {
                store.Settings.Canvas.InkWidth = 4.25;
                store.Settings.Appearance.Theme = 3;
                store.Settings.Startup.IsAutoHideCanvas = false;
                store.Save();

                Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "Settings.json")));
                Assert.IsTrue(store.FilePath.EndsWith("Settings.json", StringComparison.Ordinal));
            }

            using (var loaded = new SettingsStore(_tempDir))
            {
                loaded.Load();
                Assert.AreEqual(4.25, loaded.Settings.Canvas.InkWidth);
                Assert.AreEqual(3, loaded.Settings.Appearance.Theme);
                Assert.IsFalse(loaded.Settings.Startup.IsAutoHideCanvas);
                Assert.AreEqual(1, loaded.Settings.SchemaVersion);
                StringAssert.Contains(File.ReadAllText(loaded.FilePath), "schemaVersion");
            }
        }

        [TestMethod]
        public void LoadCorruptJson_KeepsPreviousInstance()
        {
            using (var store = new SettingsStore(_tempDir))
            {
                store.Settings.Canvas.InkWidth = 9.5;
                var previous = store.Settings;
                File.WriteAllText(store.FilePath, "{ this is not json");

                store.Load();

                Assert.AreSame(previous, store.Settings);
                Assert.AreEqual(9.5, store.Settings.Canvas.InkWidth);
            }
        }

        [TestMethod]
        public void LoadMissingFile_KeepsDefaults()
        {
            using (var store = new SettingsStore(_tempDir))
            {
                Assert.IsFalse(File.Exists(store.FilePath));
                var previous = store.Settings;

                store.Load();

                Assert.AreSame(previous, store.Settings);
                Assert.AreEqual(2.5, store.Settings.Canvas.InkWidth);
                Assert.IsTrue(store.Settings.Automation.IsAutoKillPptService);
                Assert.AreEqual(1, store.Settings.SchemaVersion);
            }
        }

        [TestMethod]
        public void ScheduleSaveThenFlush_WritesFile()
        {
            using (var store = new SettingsStore(_tempDir))
            {
                store.Settings.Canvas.InkWidth = 1;
                store.ScheduleSave();
                store.Settings.Canvas.InkWidth = 7;
                store.ScheduleSave();
                store.Flush();

                Assert.IsTrue(File.Exists(store.FilePath));
            }

            using (var loaded = new SettingsStore(_tempDir))
            {
                loaded.Load();
                Assert.AreEqual(7.0, loaded.Settings.Canvas.InkWidth);
            }
        }

        [TestMethod]
        public void SchemaVersion_DefaultsToOneWhenMissing()
        {
            Assert.AreEqual(1, new Settings().SchemaVersion);

            File.WriteAllText(Path.Combine(_tempDir, "Settings.json"),
                "{\r\n  \"canvas\": {\r\n    \"inkWidth\": 3.0\r\n  }\r\n}");

            using (var store = new SettingsStore(_tempDir))
            {
                store.Load();
                Assert.AreEqual(1, store.Settings.SchemaVersion);
                Assert.AreEqual(3.0, store.Settings.Canvas.InkWidth);
            }
        }
    }
}
