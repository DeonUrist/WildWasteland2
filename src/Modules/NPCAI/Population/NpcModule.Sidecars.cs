using System;
using System.IO;
using System.Text;

namespace WildWasteland.Modules.NPC
{
    public sealed partial class NpcModule
    {
        /// <summary>Rewrites only the senses sidecar's slot header when a verified save snapshot is copied.</summary>
        static byte[] RemapSensesSnapshot(string source, string target, byte[] bytes)
        {
            string text;
            using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true)) text = reader.ReadToEnd();
            string header = "v1 " + source;
            if (!text.StartsWith(header + "\n", StringComparison.Ordinal) && !text.StartsWith(header + "\r\n", StringComparison.Ordinal))
                throw new IOException("NPC senses companion identity is invalid");
            return Encoding.UTF8.GetBytes("v1 " + target + text.Substring(header.Length));
        }
    }
}
