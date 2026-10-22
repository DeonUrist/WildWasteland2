using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace WildWasteland.Core
{
    /// <summary>Publishes same-directory files with Windows write-through renames, preserving an optional previous version.</summary>
    internal static class AtomicFile
    {
        /// <summary>Renames a file atomically without Mono's ReplaceFile implementation.</summary>
        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool MoveFileEx(string source, string target, uint flags);

        /// <summary>Publishes a completed temporary file over the target, which may not exist yet.</summary>
        internal static void Replace(string source, string target) { Replace(source, target, null); }

        /// <summary>Publishes a completed temporary file after atomically preserving the previous target at backup, when supplied.</summary>
        internal static void Replace(string source, string target, string backup)
        {
            source = Path.GetFullPath(source); target = Path.GetFullPath(target);
            bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            CheckPaths(source, target, comparison);
            if (backup != null)
            {
                backup = Path.GetFullPath(backup);
                CheckPaths(target, backup, comparison);
                CheckPaths(source, backup, comparison);
                if (!File.Exists(source)) throw new FileNotFoundException("Atomic publication source is missing", source);
                if (File.Exists(target)) Backup(target, backup);
            }
            Publish(source, target, windows);
        }

        /// <summary>Rejects aliases and cross-directory renames before any target or backup is changed.</summary>
        static void CheckPaths(string source, string target, StringComparison comparison)
        {
            if (string.Equals(source, target, comparison)
                || !string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(target), comparison))
                throw new IOException("Atomic save publication requires distinct files in the same directory");
        }

        /// <summary>Copies the current target to a distinct temporary file and atomically publishes that completed backup.</summary>
        static void Backup(string target, string backup)
        {
            string temp = backup + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(target, temp, false);
                using (var stream = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.None)) stream.Flush(true);
                Publish(temp, backup, Environment.OSVersion.Platform == PlatformID.Win32NT);
            }
            finally
            {
                // Failed publication keeps the old target and old backup. A leftover temporary copy is safe to remove.
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        /// <summary>Replaces one file with a completed same-directory source using the native platform primitive.</summary>
        static void Publish(string source, string target, bool windows)
        {
            if (windows)
            {
                // Avoid Mono's ReplaceFile path (1175). No COPY_ALLOWED: publication stays a same-volume rename.
                if (!MoveFileEx(source, target, 1 | 8))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new IOException("Atomic save replacement failed: " + Path.GetFileName(target), new Win32Exception(error));
                }
            }
            else if (File.Exists(target)) File.Replace(source, target, null);
            else File.Move(source, target);
        }
    }
}
