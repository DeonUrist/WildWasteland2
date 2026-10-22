using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Merchant item editor access to merchant buses (cars, shop objects, save layout, freeze); Developer-facing (Wave A split of Patrols Merchants/MerchantModel.cs).
    public static class MerchantApi
    {
        public static GameObject[] Cars()=>MerchantHost.All.Where(h=>h!=null).Select(h=>h.gameObject).ToArray();
        public static MerchantObject[] Objects(GameObject car)=>car!=null?car.GetComponent<MerchantHost>()?.Objects.ToArray()??new MerchantObject[0]:new MerchantObject[0];
        public static string Save(GameObject car)=>MerchantLayouts.Save(car);
        public static void Freeze(GameObject car){var host=car!=null?car.GetComponent<MerchantHost>():null;if(host==null)return;var journey=WorldTraffic.Find(car.GetComponent<PatrolMarker>()?.TrafficId);if(journey?.merchant==true)journey.waitSeconds=300f;else host.Park();}
    }
}
