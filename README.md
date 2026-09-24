# Terra Nova: Strike Force Centauri — Mouse Freelook

Modern **mouse-look** for the 1996 DOS classic *Terra Nova: Strike Force Centauri* (Looking Glass Technologies), running in DOSBox — **Steam, GOG or standalone**.

In the original game the mouse only moves an aiming cursor. This small external tool adds real freelook:

- **mouse left/right** — turn your PBA
- **mouse up/down** — look up/down (within the game's own head limits)
- the **aiming reticle stays centred** — you fire where you look

Keyboard controls are unchanged. No game file is modified.

## Download & use

1. Download `TerraNovaFreelook_v1.0.zip` from the [Releases](../../releases) page and unzip it anywhere.
2. Run **`TNFreelook.exe`** and leave its window open (it waits for the game).
3. Start Terra Nova as usual. The window shows `Game found in ...`.
4. In a mission, press **Y** to switch freelook on (high beep), **Y** again to switch it off.
   Switch it off when you want to click the cockpit buttons with the mouse.

Freelook switches itself off whenever you leave the 3D view (options screen `O`, end of mission, menus). Press **Y** again when you are back in action.

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
| GOG "Nightdive" build, French & English exe | DOSBox Staging (64-bit, 30 MB) | ✅ |
| Standalone French v1.09 | DOSBox 0.74 (32-bit, 16 MB — every address shifted) | ✅ |

Signatures verified (exactly one match each) in: English v1.08 (GOG/Steam CD image), English v1.09, French v1.09.
Both in-game resolutions (320×200 and 320×400) are supported.
Feedback welcome for the Steam release and DOSBox-X.

## Safety

- It only changes, in the running game, values located from the game's own code: body heading, head pitch and the mouse cursor. Nothing is written until you press the toggle key during a mission, and it refuses outside the 3D view.
- It continuously checks that the 3D camera is alive and switches off otherwise (menus, options, mission end), so it never writes into stale memory.
- Because it reads/writes another program's memory, some antivirus software may flag it, like any game trainer. The full source is here: read it and build it yourself (below).

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
| Cursor frame (320×200 / 320×400) | the mouse library init |

When several matches exist (DOS/4GW can leave an unrelocated copy of the exe in memory), the one with the highest captured addresses is used — that is the relocated, running code.

---

### Français

Vue à la souris pour *Terra Nova: Strike Force Centauri* (DOS, 1996) sous DOSBox — versions Steam, GOG ou autonome.
Lancer `TNFreelook.exe`, lancer le jeu, puis **Y** en mission : souris gauche/droite = tourner, haut/bas = regarder, réticule fixe au centre. Se coupe tout seul hors de la vue 3D. Réglages dans `TNFreelook.ini`. Aucun fichier du jeu n'est modifié ; code source fourni (`src/build.bat` pour recompiler, rien à installer).
