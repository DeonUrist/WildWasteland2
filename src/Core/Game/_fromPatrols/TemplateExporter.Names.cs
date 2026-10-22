using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Patrols
{
    /// <summary>Prefab name of a scene instance (strips (Clone)N and " (N)" suffixes); used by 15 runtime files, goes to Core Names in Wave B (B01). Wave A split of Patrols Editor/TemplateExporter.cs.</summary>
    internal sealed partial class TemplateExporter
    {
        private static readonly Regex CloneSuffix = new Regex(@"(\(Clone\)\d*|\s\(\d+\))+$");
        internal static string PrefabName(string instance) { return CloneSuffix.Replace(instance ?? "", "").Trim(); }
    }
}
