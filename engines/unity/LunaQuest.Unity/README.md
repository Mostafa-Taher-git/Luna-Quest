# Luna-Quest — Unity adapter

This is the Unity 6.3 LTS side of Luna-Quest. It is a complete, self-contained project with
procedural geometry, an IMGUI HUD and the shared `LunaQuest.Core` simulation.

The project intentionally uses **only built-in Unity modules**. There is no URP/HDRP, no
third-party package and no asset-store dependency, so a fresh Unity install does not need to
download anything to open it.

---

## Open and run

1. Install **Unity 6.3 LTS** (`6000.3.0f1`) from Unity Hub.
2. In Unity Hub: **Add project from disk** → select this folder
   (`engines/unity/LunaQuest.Unity`).
3. Let the first import finish. Unity may create `Library/`, `Temp/`,
   `ProjectSettings/QualitySettings.asset` and `ProjectSettings/GraphicsSettings.asset`;
   that is normal for a project authored outside the editor.
4. Open `Assets/Scenes/Bootstrap.unity`. It is the default scene.
5. Press **Play**.

The scene contains one object, `LunaQuest`, with the `LunaQuestGame` component. Everything
else — colony, camera, player, crew, HUD, lights, stars, Earth — is created at runtime.

If the scene ever opens without the object, create an empty GameObject, add the
`LunaQuestGame` component, and press Play. That is the entire bootstrap.

### First-open checklist

- The console should print
  `Luna-Quest ready: 16 buildings, 8 crew, 15 incident rules.`
- The Game view should show the name/language screen, then the colony after **Begin the
  mission**.
- If the console reports `Content could not be loaded`, run the sync script from the
  repository root and reopen Unity:

  ```bash
  ./tools/sync-content.sh
  ```

  The content must be at `Assets/StreamingAssets/content/*.json`.

---

## Requirements

| Item | Version |
|---|---|
| Unity | 6.3 LTS (`6000.3.0f1`) |
| Render pipeline | Built-in |
| Input | Legacy Input Manager (`activeInputHandler: 0`) |
| .NET profile | .NET Standard 2.1 |
| Packages | Built-in modules only |

The project uses the old Input Manager on purpose: it is present in a default Unity 6
install and needs no package setup. If you switch the project to the Input System package,
the `ColonyPlayer` and `LunaQuestGame` key checks must be migrated.

---

## Controls

| Action | Key |
|---|---|
| Move | `W A S D` |
| Run | `Shift` |
| Jump | `Space` |
| Look | Mouse |
| Use station | `E` |
| First / third person | `V` |
| Journal | `J` |
| Resource math | `G` |
| Help | `H` |
| Save / load | `F5` / `F9` |
| Language | `L` |
| Release mouse / close panel | `Esc` |

Click the Game view to lock the mouse before walking. The start screen is drawn in IMGUI and
captures the mouse for the name field.

---

## Project layout

```text
LunaQuest.Unity/
├── Assets/
│   ├── Scenes/Bootstrap.unity        entry scene (one LunaQuestGame object)
│   ├── Scripts/                      Unity adapter scripts + asmdef
│   │   ├── ColonyWorld.cs            procedural colony, stations, crew, astronaut
│   │   ├── ColonyPlayer.cs           CharacterController, camera, suit lamp
│   │   ├── ColonyHud.cs              IMGUI HUD, journal, resource math
│   │   ├── LunaQuestGame.cs          bootstrap, content load, interaction, save
│   │   ├── VoicePlayer.cs            MP3 pack + subtitles
│   │   └── Core/                     mirror of shared/LunaQuest.Core
│   ├── Resources/
│   │   ├── Art/Astronaut/            baked OBJ + MTL
│   │   ├── Fonts/                    DejaVu Sans, Noto Naskh Arabic
│   │   ├── Textures/                 Earth texture
│   │   └── Voice/                    MP3 pack + subtitles JSON
│   └── StreamingAssets/content/      six content JSON files
├── Packages/manifest.json            built-in modules only
└── ProjectSettings/                  project version, player, input, scene list
```

`Assets/Scripts/Core` is a **generated mirror**. Edit `shared/LunaQuest.Core` in the
repository root and run `./tools/sync-content.sh`; do not edit the mirror by hand.

The astronaut is a static OBJ baked from the repaired GLB at the standing `0.4 s` pose. It
has no Animator and no runtime glTF dependency. To change the model or pose, replace
`assets/astronaut/cute_astronaut.glb` and run:

```bash
python3 tools/bake_astronaut_obj.py
```

Then let Unity reimport `Assets/Resources/Art/Astronaut/cute_astronaut_baked.obj`.

---

## Build a player

1. **File → Build Settings**.
2. The `Bootstrap` scene should already be in **Scenes In Build**. If not, add it.
3. Choose **Standalone Windows / macOS / Linux** and build.
4. The content in `Assets/StreamingAssets/content` is copied into the build automatically.
5. The voice MP3s and fonts are in `Resources`, so they are included automatically.

No scripting define symbols, no addressables and no custom packages are required.

---

## Save file

`Application.persistentDataPath/lunaquest-save.json`

Schema version 2. The format is shared with the Godot adapter: IDs and values only. Copy the
file between engines to continue the same colony.

---

## Notes and limitations

- **This project was authored without a Unity editor in the build environment.** It has been
  compiled against Unity reference assemblies with 0 warnings / 0 errors. The scene and
  project YAML are hand-authored. Unity may fill in missing default settings on first open.
- The Unity adapter uses the built-in render pipeline. It does not use URP, Shader Graph,
  Timeline, Cinemachine, TextMeshPro or the UI Toolkit.
- The HUD is IMGUI. If you replace it with uGUI or UI Toolkit, the simulation API stays
  the same.
- The astronaut in Unity is static by design. Godot uses the animated GLB frozen at the same
  pose. Re-run the baker if the source model changes.
- The voice pack is inherited from the team’s V1 prototype; its redistribution rights are
  unconfirmed. The source repository is public, but keep shipped builds private until the
  team clears them.
- Two crew names are intentionally pending team approval and display as “Name pending
  approval”.

See the repository root `README.md`, `CREDITS.md` and `LICENSE` for the full project notes.
