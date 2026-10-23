using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class SkillMerchantRuntime : ISkillMerchantRuntime
    {
        public bool Resolve(GameObject speaker,out GameObject merchant)
        {
            merchant = null; float closest = 2500;
            foreach (var host in MerchantHost.All)
            {
                if (host == null || !host.Ready) continue; float distance = (host.transform.position - speaker.transform.position).sqrMagnitude;
                if (distance < closest) { closest = distance; merchant = host.gameObject; }
            }
            return merchant != null;
        }
        public string Identity(GameObject merchant)
        {
            var host = merchant.GetComponent<MerchantHost>();
            if (string.IsNullOrEmpty(host.Data.perkIdentity)) host.Data.perkIdentity = Guid.NewGuid().ToString("N"); return host.Data.perkIdentity;
        }
        public bool Reroll(GameObject merchant,bool stock)
        { var host = merchant != null ? merchant.GetComponent<MerchantHost>() : null; return host != null && host.RerollPerk(stock); }
    }
    internal sealed partial class MerchantHost
    {
        internal bool RerollPerk(bool stock)
        {
            if (!Ready || SaveBus.Busy || !SkillPerks.Has("MerchantReroll")) return false;
            if (!stock)
            {
                if (_lists.Any(f => f == null || f.ActiveStateName != "done")) return false;
                bool changed = false; foreach (var list in _lists) changed |= ModApi.Get<ISkillMerchantChoices>()?.ResetWanted(list.gameObject) ?? false;
                if (changed) Data.demands = new MerchantDemand[0]; return changed;
            }
            ObserveStock(true);
            var next = new List<MerchantStock>(); var made = new Dictionary<MerchantStock,GameObject>();
            try
            {
                foreach (var old in Data.stock)
                {
                    var goods = ModApi.Get<IMerchantGoods>()?.Goods(MerchantCatalog.Station(old.type)); if (goods == null || goods.Length == 0) throw new InvalidOperationException("No eligible merchant stock");
                    var prefab = goods[UnityEngine.Random.Range(0,goods.Length)]; var sale = Objects.FirstOrDefault(o => o.Id == old.type + "/sale/" + old.slot)?.Transform;
                    if (prefab == null || sale == null) throw new InvalidOperationException("Merchant sale anchor unavailable");
                    var record = new MerchantStock { type = old.type, slot = old.slot, prefab = prefab.name };
                    var item = Instantiate(prefab,sale.position,sale.rotation); made.Add(record,item); item.transform.SetParent(sale,true);
                    ItemSpawner.Name(item,prefab.name); ItemSpawner.AddExisting(item,false); ItemSpawner.Commit(item);
                    foreach (var fsm in item.GetComponents<PlayMakerFSM>())
                    { if (fsm.FsmName == "Value" || fsm.FsmName == "ForBuy" || fsm.FsmName == "Buyable") { Init(fsm); if (fsm.FsmName == "Value") fsm.Fsm.SetState("value"); if (fsm.FsmName == "ForBuy") { fsm.FsmVariables.FindFsmInt("ForBuyInt").Value = 1; fsm.Fsm.SetState("forBuy"); } } }
                    var rb = item.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
                    foreach (var a in item.GetComponentsInChildren<Collider>(true)) foreach (var b in _chassis) if (a != null && b != null && a != b) Physics.IgnoreCollision(a,b,true);
                    record.nativeName = item.name; record.buyPrice = FsmUtil.Find(item,"Value")?.FsmVariables.FindFsmInt("BuyValue")?.Value ?? -1; next.Add(record);
                }
            }
            catch (Exception error)
            { foreach (var item in made.Values) if (item != null) { ItemSpawner.RemoveTree(item); item.SetActive(false); Destroy(item); } Log.Warn(null,"Merchant perk stock reroll failed: " + error.Message); return false; }
            foreach (var pair in _stock) if (!pair.Key.purchased && pair.Value != null) { ItemSpawner.RemoveTree(pair.Value); pair.Value.SetActive(false); Destroy(pair.Value); }
            _stock.Clear(); _itemFsms.Clear(); foreach (var pair in made) _stock.Add(pair.Key,pair.Value); Data.stock = next.ToArray(); return true;
        }
    }
}
