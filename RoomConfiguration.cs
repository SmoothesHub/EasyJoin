using System;
using System.IO;
using System.Text;
using System.Globalization;
using BepInEx.Configuration;

namespace EasyJoin
{
    /// <summary>Event-driven configuration shared by the computer and desktop editor.</summary>
    internal sealed class RoomConfiguration
    {
        internal const string FileName = "Code For Option 3.cfg";
        private readonly string path;
        private readonly Action<string> warn;
        private ConfigFile file;
        private ConfigEntry<string> entry;
        private ConfigEntry<string> xEntry, yEntry;
        private string lastProblem;
        private string savedCode = "";
        private float savedX = 40, savedY = 40;
        private bool codeDirty, positionDirty;
        internal string CachedCode { get; private set; }
        internal string RawCode { get; private set; } = "";
        internal float WindowX { get; private set; } = 40;
        internal float WindowY { get; private set; } = 40;
        internal bool HasPendingChanges { get { return codeDirty || positionDirty; } }
        internal bool SaveFailed { get; private set; }
        internal bool ReadFailed { get; private set; }

        internal RoomConfiguration(string path, Action<string> warn)
        {
            this.path = path;
            this.warn = warn;
            Reload();
        }

        internal bool Reload()
        {
            // A GUI edit is active immediately, including during its short save debounce.
            // A disk reload must never replace that explicit edit with the previous value.
            if (HasPendingChanges) return CachedCode != null;
            try
            {
                ReadFailed = false;
                EnsureExists();
                if (new FileInfo(path).Length > 65536)
                {
                    ReadFailed = true;
                    return Reject("Configuration exceeds 64 KiB. Use a small [EasyJoin] configuration file.");
                }

                if (file == null)
                {
                    var loadedFile = new ConfigFile(path, false) { SaveOnConfigSet = false };
                    var loadedEntry = loadedFile.Bind("EasyJoin", "CodeForOption3", "",
                        "Room code for OPTION 3 on the JOIN/ROOM tab. 1-10 letters or digits; blank disables EasyJoin.");
                    file = loadedFile;
                    entry = loadedEntry;
                    xEntry = file.Bind("GUI", "WindowX", "40", "Desktop window X position.");
                    yEntry = file.Bind("GUI", "WindowY", "40", "Desktop window Y position.");
                }
                else
                {
                    // BepInEx otherwise retains an old value when the key is removed from disk.
                    // SaveOnConfigSet stays false, so this cannot overwrite the user's file.
                    entry.Value = "";
                    xEntry.Value = yEntry.Value = "40";
                    file.Reload();
                }

                RawCode = savedCode = entry.Value;
                WindowX = savedX = ParseCoordinate(xEntry.Value);
                WindowY = savedY = ParseCoordinate(yEntry.Value);
                string normalized;
                if (!TryNormalize(entry.Value, out normalized))
                    return Reject("CodeForOption3 is empty or invalid. Use 1-10 ASCII letters/digits in [EasyJoin].");
                CachedCode = normalized;
                lastProblem = null;
                return true;
            }
            catch (Exception ex)
            {
                ReadFailed = true;
                // Never use a stale code after a failed reload or a partially saved file.
                return Reject("Could not read " + FileName + " (" + ex.GetType().Name + "). Save the file and try again.");
            }
        }

        internal void EditCode(string value)
        {
            // TextField is single-line; also defend pasted control characters at this boundary.
            value = (value ?? "").Replace("\r", "").Replace("\n", "").Replace("\0", "").Trim();
            string code;
            CachedCode = TryNormalize(value, out code) ? code : null;
            RawCode = CachedCode ?? value;
            codeDirty = RawCode != savedCode;
            SaveFailed = ReadFailed = false;
        }

        internal void SetPosition(float x, float y)
        {
            WindowX = Finite(x) ? x : 40;
            WindowY = Finite(y) ? y : 40;
            positionDirty = WindowX != savedX || WindowY != savedY;
            SaveFailed = false;
        }

        internal bool SavePending()
        {
            if (!HasPendingChanges) return true;
            string temporary = null;
            try
            {
                EnsureExists();
                if (new FileInfo(path).Length > 65536) throw new IOException("Configuration too large.");
                string original = File.ReadAllText(path);
                string changed = original;
                // Merge only the edited settings into the latest file, preserving other keys/comments.
                if (codeDirty) changed = ConfigText.Set(changed, "EasyJoin", "CodeForOption3", RawCode);
                if (positionDirty)
                {
                    changed = ConfigText.Set(changed, "GUI", "WindowX", WindowX.ToString("R", CultureInfo.InvariantCulture));
                    changed = ConfigText.Set(changed, "GUI", "WindowY", WindowY.ToString("R", CultureInfo.InvariantCulture));
                }
                if (changed != original)
                {
                    temporary = Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".tmp");
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true)) writer.Write(changed);
                        stream.Flush(true);
                    }
                    // Same-directory atomic replacement: a failed save never truncates the CFG.
                    File.Replace(temporary, path, null);
                    temporary = null;
                }
                savedCode = RawCode; savedX = WindowX; savedY = WindowY;
                codeDirty = positionDirty = SaveFailed = ReadFailed = false;
                lastProblem = null;
                return true;
            }
            catch (Exception ex)
            {
                SaveFailed = true;
                string problem = "Could not save " + FileName + " (" + ex.GetType().Name + "). The GUI edit is still active; close/reopen the GUI to retry.";
                if (problem != lastProblem) { lastProblem = problem; warn(problem); }
                return false;
            }
            finally
            {
                if (temporary != null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }

        private static float ParseCoordinate(string value)
        {
            float number;
            return Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && Finite(number) ? number : 40;
        }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }

        private void EnsureExists()
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                // CreateNew refuses to overwrite a file created concurrently by an editor.
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine("# EasyJoin - Gorilla Tag");
                    writer.WriteLine("# OPTION 3 joins this code ONLY on the JOIN/ROOM tab.");
                    writer.WriteLine("# Use 1-10 letters A-Z or digits 0-9. Case does not matter.");
                    writer.WriteLine("# Example: CodeForOption3 = ABCD");
                    writer.WriteLine("# Blank disables quick join. Save, then press OPTION 3 to reload.");
                    writer.WriteLine();
                    writer.WriteLine("[EasyJoin]");
                    writer.WriteLine("CodeForOption3 = " + (CachedCode ?? ""));
                    writer.WriteLine();
                    writer.WriteLine("[GUI]");
                    writer.WriteLine("WindowX = " + WindowX.ToString("R", CultureInfo.InvariantCulture));
                    writer.WriteLine("WindowY = " + WindowY.ToString("R", CultureInfo.InvariantCulture));
                }
            }
            catch (IOException)
            {
                if (!File.Exists(path)) throw;
            }
        }

        internal static bool TryNormalize(string value, out string code)
        {
            code = null;
            if (value == null) return false;
            value = value.Trim();
            if (value.Length == 0 || value.Length > 10) return false;
            for (int i = 0; i < value.Length; ++i)
            {
                char c = value[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
                    return false;
            }
            code = value.ToUpperInvariant();
            return true;
        }

        private bool Reject(string problem)
        {
            CachedCode = null;
            if (problem != lastProblem)
            {
                lastProblem = problem;
                warn(problem);
            }
            return false;
        }
    }
}
