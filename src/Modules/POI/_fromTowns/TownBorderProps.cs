using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Builds the deterministic dressing plan: towers, border trees/wrecks. Candidate selection happened in TownDressingPlan
    // (normally on the MapMagic worker), so every step here places one planned item; no rejected attempts cost frames.
    internal sealed class TownBorderProps
    {
        internal readonly List<Bounds> Blockers = new List<Bounds>(); // Relative to town center, for cached vehicle/walker graphs.
        private readonly TownSite _site;
        private readonly TownAssets _assets;
        private readonly Transform _root;
        private readonly TownDressingPlan _plan;
        private int _tower, _prop;
        internal bool Ready { get; private set; }
        internal float Progress { get { return Ready ? 1 : .5f * (_plan.Towers.Length == 0 ? 1 : (_tower + (_tower < _plan.Towers.Length ? _assets.WallKit.Tower.Progress : 0)) / (float)_plan.Towers.Length)
            + .5f * (_plan.Props.Length == 0 ? 1 : _prop / (float)_plan.Props.Length); } }
        internal string Stage { get { return _tower < _plan.Towers.Length ? _assets.WallKit.Tower.Ready ? "Placing town towers" : "Preparing tower navigation" : "Placing town trees and wrecks"; } }
        internal TownBorderProps(TownSite site, TownAssets assets, Transform root)
        {
            _site = site; _assets = assets; _root = root;
            _plan = TownOverlayPlan.DressingFor(site.Plan, assets);
            Blockers.AddRange(_plan.Blockers);
        }
        private Vector3 Scene(Vector3 p) { return _site.Ground(_site.Anchor.TransformPoint(_site.Plan.WorldCenter + p), p.y); }
        internal bool Step(out bool spawned)
        {
            spawned = false;
            if (Ready) return false;
            var kit = _assets.WallKit;
            if (_tower < _plan.Towers.Length)
            {
                if (!kit.Tower.Ready) return kit.Tower.Step();
                var item = _plan.Towers[_tower++];
                var tower = UnityEngine.Object.Instantiate(kit.Tower.Prefab, _root, false); tower.name = kit.Tower.Prefab.name;
                tower.transform.SetPositionAndRotation(Scene(item.Position), _site.Anchor.rotation * Quaternion.Euler(0, item.Yaw, 0)); tower.SetActive(true);
                Add(item.Box); TownBakeBridge.Discover(tower); spawned = true;
                return true;
            }
            if (_prop >= _plan.Props.Length)
            {
                Ready = true; Physics.SyncTransforms();
                Log.Always("[Towns] border " + _site.Plan.Key + ": " + _plan.Towers.Length + "/" + _plan.TowerTarget + " small towers with inward ladders, " + _plan.Props.Length + " static wrecks/trees from the plan; independent tower map reused.");
                return true;
            }
            var prop = _plan.Props[_prop++];
            GameObject go;
            if (prop.Kind == 1)
            {
                if (prop.Index >= kit.Trees.Count) return true;
                go = UnityEngine.Object.Instantiate(kit.Trees[prop.Index], _root, false);
                go.name = "Towns border tree " + (_prop - 1);
                go.transform.SetPositionAndRotation(Scene(prop.Position), _site.Anchor.rotation * Quaternion.Euler(0, prop.Yaw, 0)); go.SetActive(true);
            }
            else
            {
                if (prop.Index >= _assets.CarShells.Count) return true;
                var shell = _assets.CarShells[prop.Index]; var bounds = shell.Mesh.bounds;
                go = new GameObject("Towns border car remains " + (_prop - 1)); go.transform.SetParent(_root, false);
                go.transform.localScale = Vector3.one * prop.Scale; go.transform.rotation = _site.Anchor.rotation * Quaternion.Euler(0, prop.Yaw, 0);
                go.transform.position = Scene(prop.Position);
                go.AddComponent<MeshFilter>().sharedMesh = shell.Mesh; go.AddComponent<MeshRenderer>().sharedMaterials = shell.Materials;
                var box = go.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
            }
            Add(prop.Box); return true;
        }
        private void Add(Bounds box)
        {
            // Plan boxes are town-centred; the overlap fallback and street-edge rechecks read anchor-relative boxes.
            float offset = box.center.y; box.center += _site.Plan.WorldCenter;
            box.center = _site.Anchor.InverseTransformPoint(_site.Ground(_site.Anchor.TransformPoint(box.center), offset)); box.Expand(.3f); _site.DebrisBlockers.Add(box);
        }
    }
}
