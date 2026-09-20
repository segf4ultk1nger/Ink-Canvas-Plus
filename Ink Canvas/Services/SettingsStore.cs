using InkCanvasPlus.Helpers;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;

namespace InkCanvasPlus.Services
{
    /// <summary>
    /// Reads and writes Settings.json with a 500ms save debounce.
    /// Construct with a root directory so tests can use a temp folder instead of App.RootPath.
    /// </summary>
    public class SettingsStore : IDisposable
    {
        public const string FileName = "Settings.json";
        public const int CurrentSchemaVersion = 1;
        public const int SaveDebounceMilliseconds = 500;

        /// <summary>
        /// Application instance, if one has been registered. Isolated test stores leave this null.
        /// </summary>
        public static SettingsStore Current { get; private set; }

        private readonly object _sync = new object();
        private readonly string _settingsPath;
        private Settings _settings = new Settings();
        private Timer _debounceTimer;
        private bool _disposed;

        public SettingsStore(string rootPath) : this(rootPath, false)
        {
        }

        public SettingsStore(string rootPath, bool registerAsCurrent)
        {
            if (rootPath == null) throw new ArgumentNullException(nameof(rootPath));
            _settingsPath = CombineRootPath(rootPath);
            if (registerAsCurrent)
            {
                Current = this;
            }
        }

        public string FilePath => _settingsPath;

        public Settings Settings
        {
            get
            {
                lock (_sync)
                {
                    return _settings;
                }
            }
            set
            {
                if (value == null) return;
                lock (_sync)
                {
                    _settings = value;
                }
            }
        }

        /// <summary>
        /// Cancels a pending debounce and writes immediately. Safe when no app store exists yet.
        /// </summary>
        public static void FlushCurrentIfAny()
        {
            try
            {
                Current?.Flush();
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }

        public void Load()
        {
            lock (_sync)
            {
                if (_disposed) return;
                if (!File.Exists(_settingsPath))
                {
                    return;
                }

                try
                {
                    string text = File.ReadAllText(_settingsPath);
                    var loaded = JsonConvert.DeserializeObject<Settings>(text);
                    if (loaded != null)
                    {
                        _settings = loaded;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile("LoadSettings failed to deserialize: " + _settingsPath, LogHelper.LogType.Error);
                    LogHelper.NewLog(ex);
                }
            }
        }

        public void Save()
        {
            lock (_sync)
            {
                SaveUnlocked();
            }
        }

        /// <summary>
        /// Last call wins. The file is written ~500ms after the most recent ScheduleSave, unless Flush runs first.
        /// </summary>
        public void ScheduleSave()
        {
            lock (_sync)
            {
                if (_disposed) return;
                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(OnDebounceElapsed, null, SaveDebounceMilliseconds, Timeout.Infinite);
                }
                else
                {
                    _debounceTimer.Change(SaveDebounceMilliseconds, Timeout.Infinite);
                }
            }
        }

        public void Flush()
        {
            lock (_sync)
            {
                if (_disposed) return;
                StopTimerLocked();
                SaveUnlocked();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                StopTimerLocked();
                if (_debounceTimer != null)
                {
                    _debounceTimer.Dispose();
                    _debounceTimer = null;
                }
            }
        }

        private void OnDebounceElapsed(object state)
        {
            Save();
        }

        private void StopTimerLocked()
        {
            if (_debounceTimer != null)
            {
                _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void SaveUnlocked()
        {
            if (_disposed) return;

            string text;
            try
            {
                text = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("SaveSettingsToFile failed: " + _settingsPath, LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(_settingsPath, text);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("SaveSettingsToFile failed: " + _settingsPath, LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        private static string CombineRootPath(string rootPath)
        {
            if (rootPath.Length == 0)
            {
                throw new ArgumentException("Root path is required.", nameof(rootPath));
            }

            char last = rootPath[rootPath.Length - 1];
            if (last != Path.DirectorySeparatorChar && last != Path.AltDirectorySeparatorChar)
            {
                return rootPath + Path.DirectorySeparatorChar + FileName;
            }

            return rootPath + FileName;
        }
    }
}
