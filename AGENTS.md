# AGENTS.md — Notes for AI agents (gregMod.MusicPlayer)

Repo: gregMod.MusicPlayer · License: see `LICENSE` if present, else Apache-2.0 · Version: see `VERSION`.

MelonMod for Data Center (`MusicPlayerMod : MelonMod`). In-game music
playback. Panel key: **F9**.

## Duties

1. **Read first:** `README.md`, `src/` — only then make changes.
2. **Do not commit secrets** (keys, tokens, `.env`). Use keys only via environment variables.
3. **Preserve history:** no `push --force`, no history rewrite without instruction.
4. **Verify changes:** before reporting done, build the mod (`dotnet build gregMod.MusicPlayer.csproj -c Release` or `./build.sh MusicPlayer` from `ModRepositories/`).
5. **Keep docs in sync:** for new features update `README.md` + `CHANGELOG.md` (Unreleased).
6. **Conventions:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), one logical change per commit.
7. **When unsure:** stop and ask instead of guessing — especially for deletes, migrations, CI.

## Build and references

- Target: `net6.0`, x64. Game: Data Center (`MelonGame("Waseku", "Data Center")`).
- `references/` holds absolute symlinks into the Steam Data Center install.
  Never commit `references/*.dll`, `bin/`, `obj/`, or audio files.
- After a fresh clone, run `../tools/sync-melon-assemblies.sh`.
- Deploy only with `./build.sh MusicPlayer --deploy`.

## Hard rules (IL2CPP audio)

- **Never** engine audio decode (`UnityWebRequestAudio`, `AudioClip`
  streaming) — stripped on IL2CPP. Managed decode only (same constraint as
  gregMod.MemeRoulette `MemeDecoder`).
- **Never** touch gregCore types outside a dedicated bridge file behind a
  soft probe (JIT split) — the mod must run without `gregCore.dll`.
- **Panel key is F9** (F7 = Trainer, F8 = MultiCable, F10 = NotesHUD).
  Do not collide.
- Defensive `try/catch` in every per-frame/playback path; one failing track
  never breaks playback of the rest.

## Layout

- `src/MusicPlayerMod.cs` — MelonMod entry, prefs, toggle, scene hooks.
- `src/Core/` — playback engine, playlist, settings.
- `src/UI/` — F9 panel/overlay.
