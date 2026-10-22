using System;
using System.Collections.Generic;
using WildWasteland.Core;

namespace WildWasteland.Modules.Quests
{
    /// <summary>Quest journal serialization inside the world save, using the shared save transaction.</summary>
    internal sealed partial class Plugin : EngineSettings
    {
        const string SaveKey = "ApocalypterQuests";

        // Cooldown stamps that have run out are no use any more; dropping them keeps the saved data small over a long game.
        void PruneStamps()
        {
            double now = Game.GameHours();
            if (now < 0.0 || book.GiverStamp.Count == 0) return;
            List<string> drop = null;
            foreach (KeyValuePair<string, long> kv in book.GiverStamp)
            {
                double age = now - kv.Value / 1000.0;
                double keep = kv.Key.StartsWith("camp:", StringComparison.Ordinal) ? Takeover.GarrisonCooldownHours : CooldownHours;
                if (age < 0.0 || age > keep + 1.0) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); }
            }
            if (drop != null) foreach (string k in drop) book.GiverStamp.Remove(k);
        }

        /// <summary>Captures the journal before native serialization; Core verifies it before publication.</summary>
        void SaveBook()
        {
            if (!ready || _sessionSlot == null || _sessionSlot != SaveBus.Slot) return;
            if (saveBlocked)
            { SaveBus.ReportFailure(_saveOwner, "Quest data is unreadable; preserving the previous save."); return; }
            try
            {
                PruneStamps(); CaptureBook();
                SaveBus.Key(SaveKey).Write(_saveOwner, _sessionSlot, book.Serialize());
            }
            catch (Exception e)
            { SaveBus.ReportFailure(_saveOwner, "Quest save failed: " + e.Message); Warn("Quest save failed: " + e.Message); }
        }

        /// <summary>Loads only the journal in this save; unreadable data blocks replacement.</summary>
        void LoadBook()
        {
            book.Clear(); saveBlocked = false;
            try
            {
                string data = SaveBus.Key(SaveKey).Read(_sessionSlot);
                if (data == null) return;
                if (!book.Deserialize(data)) throw new InvalidOperationException("Invalid quest journal");
                RestoreBook(); MapLink.Reset();
                Info("Loaded " + book.Active.Count + " quests from " + _sessionSlot + ".");
            }
            catch (Exception e)
            { saveBlocked = true; book.Clear(); Warn("Quest data could not be read; saving blocked: " + e.Message); }
        }
    }
}
