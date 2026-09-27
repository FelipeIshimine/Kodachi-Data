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

It only works in Play Mode, with a `DataServiceInstaller` in the scene; otherwise it says what is
missing. The window reads through that installer's `DataRepository`, so it sees exactly what the game
sees.

- **Refresh** reads the profiles and keys again.
- Each profile lists **Profile Data** (data kept for that profile) and **Sessions** (data kept per
  session). "(none)" means that section is empty.
- Click a key to show its value, read-only, in the **Value** pane. "(not found)" means the key is
  listed but has no data.

The window only reads; it never changes saved data.
