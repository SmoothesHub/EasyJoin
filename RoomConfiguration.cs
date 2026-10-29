using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;

namespace EasyJoin
{
    /// <summary>Reads only on startup and an eligible OPTION 3 press. Never saves over user data.</summary>
    internal sealed class RoomConfiguration
    {
        internal const string FileName = "Code For Option 3.cfg";
        private readonly string path;
        private readonly Action<string> warn;
        private ConfigFile file;
        private ConfigEntry<string> entry;
        private string lastProblem;
        internal string CachedCode { get; private set; }

        internal RoomConfiguration(string path, Action<string> warn)
        {
            this.path = path;
            this.warn = warn;
            Reload();
        }

        internal bool Reload()
        {
            try
            {
                EnsureExists();
                if (new FileInfo(path).Length > 65536)
                    return Reject("Configuration exceeds 64 KiB. Use a small [EasyJoin] configuration file.");

                if (file == null)
                {
                    var loadedFile = new ConfigFile(path, false) { SaveOnConfigSet = false };
                    var loadedEntry = loadedFile.Bind("EasyJoin", "CodeForOption3", "",
                        "Room code for OPTION 3 on the JOIN/ROOM tab. 1-10 letters or digits; blank disables EasyJoin.");
                    file = loadedFile;
                    entry = loadedEntry;
                }
                else
                {
                    // BepInEx otherwise retains an old value when the key is removed from disk.
                    // SaveOnConfigSet stays false, so this cannot overwrite the user's file.
                    entry.Value = "";
                    file.Reload();
                }

                string normalized;
                if (!TryNormalize(entry.Value, out normalized))
                    return Reject("CodeForOption3 is empty or invalid. Use 1-10 ASCII letters/digits in [EasyJoin].");
                CachedCode = normalized;
                lastProblem = null;
                return true;
            }
            catch (Exception ex)
            {
                // Never use a stale code after a failed reload or a partially saved file.
                return Reject("Could not read " + FileName + " (" + ex.GetType().Name + "). Save the file and try again.");
            }
        }

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
