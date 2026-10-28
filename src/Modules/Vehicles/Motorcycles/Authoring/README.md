# Motorcycle rack

`MotoRack.blend` is Denis's `C:\downloads\MotoRack.blend`, supplied 2026-10-28.
Only Rack, Bags and Collider enter the rack export. Both native toolsets retain
their meshes, interaction FSMs and references; their exact authored local poses
are exported into `MotoRack.layout.json`.

Run the existing Blender executable in background mode:

```powershell
& 'C:\Soft\Blender\blender.exe' --background --factory-startup --disable-autoexec --python src/Modules/Vehicles/Motorcycles/Authoring/export.py -- src/Modules/Vehicles/Motorcycles/Authoring/MotoRack.blend src/Modules/Vehicles/Embedded/Motorcycles/MotoRack.glb
```

The source uses Sigma's Blender frame. Unity coordinates are `(-x, z, -y)`;
the standard glTF export and Core.Glb's X reflection give this same frame.
The export restores the authored native metal_rusted_31/fabric_10 textures from
the offline game asset dump, embedding them as a fallback. Runtime prefers
matching native Standard materials, retaining their normal maps and finish.

Collider's authored open basket/bags are decomposed into five convex hulls each
(bottom and four walls). Bent support rails are divided at their authored bends.
All 31 hulls stay hollow around storage, unlike a single convex hull of Collider.
Three inset cargo boxes follow the actual interior orientation. At runtime their
objects are named PhysicsLock on layer 18, so native LockPhysics, vehicle cargo
recognition and existing save handling continue to apply. Existing chassis
PhysicsLock colliders are disabled, retaining their native local references.
The existing Vehicles deterministic RefTree mechanism assigns only new rack
objects/components new local references; the motorcycle prefab ID is unchanged.

MotorcycleStarter substitutes only the starter supplies' homemade Poloska seat
when CarSpawn_StarterBase owns a Motorcycle clone. After the native second-wheel
preparation, it enters the existing motorcycle-radiator state before completion.
Native spawning still owns names, condition, saved-item registration and OnOff.
Other starter cars and authored patrol loadouts keep their own parts.

No new configuration. The previous temporary toolset sliders are superseded by
the supplied Blender poses; old tools.cfg values are ignored.

Status: Testing. Denis checks a new Motorcycle start, both toolsets, loading small
items into both bags/upper basket (allow at least three real seconds to lock),
driving with cargo, and saving/reloading. Also check a factory Motorcycle spawn
and an ordinary Poloska start. The game is never launched by the authoring script.
Expected log prefixes: `Motorcycle rack ready:` and `Motorcycle starter supplies:`.
