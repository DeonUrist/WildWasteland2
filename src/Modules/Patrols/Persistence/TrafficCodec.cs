using System;
using WildWasteland.Core;
namespace WildWasteland.Modules.Patrols
{
    /// <summary>Typed Patrols persistence facade over Core's shared record codec.</summary>
    [Obsolete("refactor: use Core.RecordJson")]
    internal static class TrafficCodec
    {
        /// <summary>Serializes the current traffic record using the shared current-format codec.</summary>
        internal static string Write(TrafficSave save) { return RecordJson.WriteRecord(save); }
        /// <summary>Reads a current-format traffic record.</summary>
        internal static TrafficSave Read(string json) { return RecordJson.ReadRecord<TrafficSave>(json); }
        /// <summary>Writes a public-field record.</summary>
        internal static string WriteRecord(object record) { return RecordJson.WriteRecord(record); }
        /// <summary>Reads a public-field record.</summary>
        internal static T ReadRecord<T>(string json) { return RecordJson.ReadRecord<T>(json); }
    }
}
