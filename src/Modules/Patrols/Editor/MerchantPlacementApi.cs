using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Merchant item editor access to merchant buses (cars, shop objects, save layout, freeze); Developer-facing (Wave A split of Patrols Merchants/MerchantModel.cs).
    internal sealed class MerchantPlacementApi : Core.IMerchantPlacement
    {
        /// <summary>Copies actual live native vehicles eligible for this authoring operation.</summary>
        public GameObject[] Cars()=>MerchantHost.All.Where(h=>h!=null).Select(h=>h.gameObject).ToArray();
        /// <summary>Copies detached merchant placement rows from the actual native merchant host.</summary>
        public Core.MerchantPlacement[] Objects(GameObject car) {var host=car!=null?car.GetComponent<MerchantHost>():null;return host!=null?host.Objects.Select(o=>new Core.MerchantPlacement {Id=o.Id,Label=o.Label,Kind=o.Kind,Transform=o.Transform}).ToArray():new Core.MerchantPlacement[0];}
        /// <summary>Writes the actual current merchant layout through its Patrols owner.</summary>
        public string Save(GameObject car)=>MerchantLayouts.Save(car);
        /// <summary>Holds the actual native merchant journey still during placement edits.</summary>
        public void Freeze(GameObject car){var host=car!=null?car.GetComponent<MerchantHost>():null;if(host==null)return;var journey=WorldTraffic.Find(car.GetComponent<PatrolMarker>()?.TrafficId);if(journey?.merchant==true)journey.waitSeconds=300f;else host.Park();}
    }
}
