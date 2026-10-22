# Ladder climbing animation

Current input: Denis's `C:/Downloads/Climbing.fbx` (Mixamo "Climbing", Beta skin, 65 mixamorig bones, frames 1-90 at 30 Hz,
hands on the ladder sides), SHA256 `93EB3CF8CD87398EEA5426740A7917012EFFDD1463E9F0C69ED55A1EC9CE0F3B`.
(The first version used `Assets/Climbing Ladder.fbx`, hands on the rungs; replaced at Denis's request.)

No Unity editor is needed. `export.py` runs in headless Blender 4.0 (with numpy):

    blender -b --python export.py -- Climbing.fbx climbing-ladder.wwpose 30

Frame 90 is frame 1 one cycle higher (1.165 m up, 0.43 m forward: the source ladder leans 20.2 degrees). The export rotates that lean
out (vertical cycle of 1.242 m = two rungs of ~0.62 m), removes the climb so the loop is in place, starts the loop at frame 30
(both feet planted, left sole on a rung at the root) and maps Blender (x, y, z) to Unity (-x, z, -y) so the T-pose faces +Z with the
left hand at -X (Unity's AvatarBuilder convention; the opposite facing bends knees and shoulders backwards).

Format v3 (little endian): "WWPA", version 3, bone count, loop frame count (89), fps, rise per cycle, hips-to-hands depth (0.333),
hips-to-toes depth (0.442); bone names (u16 length + UTF-8) with parent index (parents first); the rest pose (soles at y 0) and each
loop frame as world position (3 floats) + rotation (x, y, z, w) per bone, metres; then one byte per loop frame per foot (left, then right): how planted it is (0-255), used by the foot-on-rung IK. The runtime (PlayerBody/Climbing/PoseClip.cs)
rebuilds the skeleton once, builds a humanoid Avatar with AvatarBuilder, reads every frame back as HumanPose muscles and plays them
through HumanPoseHandler. Ladders.cs maps the phase to the ladder mesh's real rungs (ladder_M first rung -1.5545, step 0.6309;
ladder_S -0.6005, 0.636; measured from Assets/raw/Mesh) and puts the balls of the feet over the ladder line.
Output SHA256 `8AA6E93496C8DDF546B9824D7DCAFD75F84388BFFF3117247552B632566120C0`, 165847 bytes. Crane (crane.001) ladders: bars every 0.75 m, found at runtime by a sphere cast down the ladder.
