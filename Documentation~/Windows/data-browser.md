# Data Browser window

**Kodachi > Data Browser**

The Data Browser shows **what the game has saved**: every player profile, the keys stored under it,
and the value behind each key. Use it to check what a save really contains without digging through
files.

## Example

```text
Profile: default
  Profile Data
    settings  (v2)
    unlocks   (unversioned)
  Sessions
    run-2026-09-27  (v1)
```

Click `settings` and its stored value appears on the right. `(v2)` means the data was saved at version
2 of its format; `(unversioned)` means no version was recorded for it.

## Use

Pick where to read from with **Source**:

- **Live (Play Mode)** reads through the scene's `DataServiceInstaller`, so it sees exactly what the
  running game sees. Outside Play Mode, or without the installer, the window says what is missing.
- **A backend** (Binary File, PlayerPrefs, …) reads that storage directly, in or out of Play Mode.
  Backends that need a platform SDK first (CrazyGames, YouTube) are not listed. **Format** picks how
  records are decoded (JSON by default), and should match the game's `DataServiceInstaller`.

Then:

- **Refresh** reads the profiles and keys again.
- Each profile lists **Profile Data** (data kept for that profile) and **Sessions** (data kept per
  session). "(none)" means that section is empty.
- Click a key to show its value, read-only, in the **Value** pane. JSON records show as text; other
  binary data shows its size and first bytes. "(not found)" means the key is listed but has no data.
- **Delete** (above the value) removes that key and its version record from the selected source, after
  a confirmation. A profile-data key is also dropped from the profile's index; a session is dropped from
  the session list. It cannot be undone.

### Previews for other data

A package can show its own records better by implementing `IDataBrowserPreview`
(`KodachiGames.Data.Editor`) with a public parameterless constructor; the window finds it
automatically. `CanPreview(bytes)` says whether it recognizes the record, and `CreatePreview(key, bytes)`
returns the UI Toolkit element shown in the Value pane.

Kodachi-ECS ships one for **binary world saves** (the level saves): it lists every block with its kind,
entry count and size, largest first, and **Export JSON…** writes the whole save as a readable
`JsonWorldFormat` file.

Apart from **Delete**, the window only reads.
