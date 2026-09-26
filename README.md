# Terra Nova: Strike Force Centauri — Mouse Freelook

Modern **mouse-look** for the 1996 DOS classic *Terra Nova: Strike Force Centauri* (Looking Glass Technologies), running in DOSBox — **Steam, GOG or standalone**.

In the original game the mouse only moves an aiming cursor. This small external tool adds real freelook:

- **mouse left/right** — turn your PBA
- **mouse up/down** — look up/down (within the game's own head limits)
- the **aiming reticle stays centred** — you fire where you look

Keyboard controls are unchanged. No game file is modified.

**Works with the Steam version** as well as GOG and standalone installs.

> The same freelook is also part of **[Terra Nova Plus](https://github.com/skw-1337/terra-nova-plus)**, an all-in-one pack that adds noclip, a longer view distance, forced 320×400 and a true 16:9 widescreen mode.

## What's new in v1.1

- **No more screen shake when standing still.** v1.0 checked twice a second that the 3D view was alive by nudging your heading by 0.18°; that nudge was visible. The check now happens once, when you switch freelook on.
- **Smoother.** The tool now runs at ~500 updates per second (1 ms Windows timer) instead of ~64, so mouse movement is applied finely and evenly.
- **No more reticle trails on the cockpit.** The game's mouse interrupt handler used to redraw the reticle wherever the mouse drifted between two re-centrings; the tool now uses the game's own "cursor frozen" flag while freelook is on. Mouse clicks (firing) still work.
- **`O` or `Esc` switch freelook off** instead of waiting for a timeout: the cursor is free right away in the options screen.
- **Dual screens:** while freelook is on, the Windows pointer is kept inside the game window, so it cannot slip onto a second monitor.

## Demo video

[![Terra Nova Mouse Freelook v1.0 — demo video](https://img.youtube.com/vi/YUgYPfcOk7U/maxresdefault.jpg)](https://www.youtube.com/watch?v=YUgYPfcOk7U)

▶ [Watch the demo on YouTube](https://www.youtube.com/watch?v=YUgYPfcOk7U) (recorded with v1.0)

## Download & use

1. Download `TerraNovaFreelook_v1.1.zip` from the [Releases](../../releases) page and unzip it anywhere.
2. Run **`TNFreelook.exe`** and leave its window open (it waits for the game).
3. Start Terra Nova as usual. The window shows `Game found in ...`.
4. In a mission, press **Y** to switch freelook on (high beep), **Y** again to switch it off.
   Switch it off when you want to click the cockpit buttons with the mouse.

Freelook switches itself off when you open the options screen (`O`) or press `Esc`, and when the mission ends. Press **Y** again when you are back in action.

SHA-256 of `TNFreelook.exe` v1.1: `FE21C8F3F3B5B9B877221286BA3274046262658FD41A8DA54EBF7117FAF106AF`

## Settings

`TNFreelook.ini` (created next to the exe on first run):

| Key | Default | Meaning |
|---|---|---|
| `sensitivity_x` | `12` | turn speed (heading units per mouse count, 65536 = 360°) |
| `sensitivity_y` | `8` | vertical look speed |
| `invert_y` | `0` | `1` = inverted vertical look |
| `toggle_scancode` | `15` | toggle key as a **physical key scancode** (hex): `15` = Y on QWERTY/AZERTY (the Z key on QWERTZ), `29` = key left of 1, `3B`–`44` = F1–F10 |
| `sound` | `1` | `0` = no beeps |

## Compatibility

The tool does **not** use fixed memory addresses. It locates the game inside the emulator through **code signatures** (instruction patterns with wildcarded addresses), then reads the real addresses from the game's own instructions. So it works with any DOSBox-family emulator and any memory setting.

| Tested live | Emulator | Result |
|---|---|---|
| GOG "Nightdive" build, French exe (v1.1) | DOSBox Staging (64-bit, 30 MB) | ✅ |
| Steam release (Nightdive build, English exe) (v1.0) | DOSBox Staging (bundled) | ✅ |
| GOG "Nightdive" build, English exe (v1.0) | DOSBox Staging | ✅ |
| Standalone French v1.09 (v1.0) | DOSBox 0.74 (32-bit, 16 MB — every address shifted) | ✅ |

All signatures, including the new v1.1 one, verified (exactly one match each) in: English v1.08 (GOG/Steam CD image), English v1.09 (Steam and GOG executables), French v1.09.
Both in-game resolutions (320×200 and 320×400) are supported.
Feedback welcome for DOSBox-X.

## Safety

- It only changes, in the running game, values located from the game's own code: body heading, head pitch, the mouse cursor and the game's "cursor frozen" flag. Nothing is written until you press the toggle key during a mission, and it refuses outside the 3D view.
- It switches off by itself in menus (`O`, `Esc`), when the mission ends, and if the 3D camera stops following your mouse — so it never writes into stale memory.
- Because it reads/writes another program's memory, some antivirus software may flag it, like any game trainer. The full source is here: read it and build it yourself (below).

## Anti-cheat note

The tool only opens DOSBox processes and never touches any other program. Still, it edits another process's memory, which is what game trainers do, and some anti-cheat systems (especially kernel-level ones) watch the whole PC. **Close the tool before playing online games protected by an anti-cheat.** It costs nothing and removes any doubt.

## Build from source

No install needed — Windows ships the C# compiler (.NET Framework 4):

```bat
cd src
build.bat
```

or manually:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize /out:TNFreelook.exe TNFreelook.cs
```

## How it works (short)

| What | Found through |
|---|---|
| Emulated RAM base | host address of the text `Memory trash: hazeRadius…` minus the address pushed by the game's `checkHazingOffset` code |
| Player id, object table, head pitch, 3D camera heading | the camera-update routine (`mov edx,[player] … add eax,<object table> … mov ax,[pitch] …`) |
| Mouse cursor, "push cursor to driver" flag | the game's mouse library (setting the flag makes the game re-centre the driver and skip reading the mouse → the reticle stays fixed) |
| "Cursor frozen" flag (v1.1) | the game's mouse interrupt handler (while set, mouse motion events are ignored and the reticle is not redrawn) |
| Cursor frame (320×200 / 320×400) | the mouse library init |

When several matches exist (DOS/4GW can leave an unrelocated copy of the exe in memory), the one with the highest captured addresses is used — that is the relocated, running code.

## See also

**[Terra Nova Plus](https://github.com/skw-1337/terra-nova-plus)** — this freelook plus noclip, view distance, forced 320×400 and a 16:9 widescreen mode, chosen in a small menu before launching the game.

---

### Français

Vue à la souris pour *Terra Nova: Strike Force Centauri* (DOS, 1996) sous DOSBox — versions Steam, GOG ou autonome.
Vidéo de démonstration : https://www.youtube.com/watch?v=YUgYPfcOk7U
Lancer `TNFreelook.exe`, lancer le jeu, puis **Y** en mission : souris gauche/droite = tourner, haut/bas = regarder, réticule fixe au centre. Se coupe tout seul avec `O` / `Échap` et en fin de mission. Réglages dans `TNFreelook.ini`. Aucun fichier du jeu n'est modifié ; code source fourni (`src/build.bat` pour recompiler, rien à installer).
**Nouveautés v1.1** : plus de tremblement à l'arrêt, mouvements plus fluides, plus de traces du réticule sur le cockpit, pointeur gardé dans la fenêtre (double écran).
**Anti-cheat** : l'outil ne touche qu'à DOSBox, mais fermez-le avant de jouer en ligne à des jeux protégés par un anti-cheat.
Envie de plus ? **[Terra Nova Plus](https://github.com/skw-1337/terra-nova-plus)** ajoute noclip, distance de vue, 320×400 forcé et écran large 16:9.
