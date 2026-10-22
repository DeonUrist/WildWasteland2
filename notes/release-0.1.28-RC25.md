# Wild Wasteland 0.1.28 RC25

This experimental prerelease fixes a conflict between the Performance module and Burner's Utilities. In-game gameplay and performance verification remains Testing.

- With Burner's Utilities `[PlayMaker] FastEvents` on and WW Performance on, every click on a car-panel control was delivered twice: headlights and cassette Play/Stop flipped and immediately flipped back. Both mods replaced the same two PlayMaker dispatch methods and each sent the event.
- PM2 Targeted events now checks those methods when it starts. If another mod already patches them, PM2 leaves dispatch to that mod and logs `[Performance] PM2: Fsm event dispatch is already patched by <id> ... PM2 not patched`; the Developer Performance page shows the same. If a mod patches them later, PM2's prefixes run last and stand down when the event was already sent.
- No config, save-format or asset changes. All RC24 assets are retained.

Install the full archive into `BepInEx/plugins`, or replace only `WildWasteland/WildWasteland.dll` over RC24.

## Check after installing

1. Confirm `Loading [Wild Wasteland 0.1.28]` in the BepInEx log.
2. With Burner's Utilities installed (FastEvents on): the log shows the PM2 line above. In a car, the headlight switch and cassette Play/Stop toggle once per click, with one click sound.
3. Without Burner's Utilities (or FastEvents off): no PM2 line; the same controls still toggle once per click.
