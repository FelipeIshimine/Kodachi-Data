# Kodachi Data

Typed data management layer on top of [Kodachi Persistence](../Kodachi-Persistence). Defines the four data categories every game needs, manages profile and session addressing, handles save versioning and migrations, and provides an editor browser to inspect saved data.

## The four data categories

| Category | Scope | Survives | Examples |
|---|---|---|---|
| **DesignData** | Game build | N/A — baked in | Balance, enemy stats, level layouts |
| **DeviceData** | This device only | Reinstall (sometimes) | Graphics, FPS, last-used profile pointer |
| **ProfileData** | This profile | Across devices (if cloud) | Wallet, unlocks, best scores, cosmetics |
| **SessionData** | This session slot | Across devices (if cloud) | Active play session state |

The killer test for DeviceData vs ProfileData: *if the player signs in on a new device, what comes along?* ProfileData/SessionData do. DeviceData doesn't.

The split between ProfileData and SessionData: ProfileData persists across all sessions for a profile (the player's account-level state). SessionData is scoped to a single play session — start a new session, get a fresh slot.

> **Note:** DesignData is not handled by this package — it's compiled into the build via ScriptableObjects or a baked binary. This package handles only runtime-persistent data.

## Key concepts

| Type | Role |
|---|---|
| `DataRepository` | The only type consumers reference. Save/load/delete for profile, session, and device data. |
| `DataContext` | Holds current profile and session IDs. Builds all persistence keys. |
| `ISaveData` | Marker interface. Every type passed to `SaveProfileData` / `LoadProfileData` / `SaveSessionData` / `SaveDeviceData` / etc. must implement it. |
| `MigrationRegistry` | Internal. Discovers `Vn` nested classes and `MigrateFrom` methods via reflection. Walks the chain on load. |
| `ProfileIndex` / `SessionIndex` / `ProfileDataIndex` | Plain serializable index objects, auto-maintained by the repository so it can list and clean up without backend enumeration support. |
| `DataServiceInstaller` | MonoBehaviour that wires `DataContext` + `DataRepository` to the ServiceLocator. |

## Quick start

1. Add a `PersistenceServiceInstaller` to your global ServiceLocator GameObject (from the Persistence package).
2. Add a `DataServiceInstaller` to the same GameObject (or a child).
3. Define a save bag that implements `ISaveData`:

```csharp
[Serializable]
public class WalletSave : ISaveData
{
    public int Gold;
    public int Gems;
}
```

4. Resolve `DataRepository` from anywhere:

```csharp
ServiceLocator.For(this).Get<DataRepository>(out var data);

// Single-profile case — zero setup needed. Defaults to profile "default", session "default".
data.SaveProfileData("wallet", new WalletSave { Gold = 500 });
data.SaveSessionData(new SessionState { Level = 3 });
data.SaveDeviceData("graphics", new GraphicsConfig { ... });

// Loading
var wallet = data.LoadProfileData<WalletSave>("wallet");
var state  = data.LoadSessionData<SessionState>();
```

## Multi-profile and multi-session

```csharp
// Create-if-missing + set active, in one call:
await data.UseProfileAsync("alice");
await data.UseSessionAsync("slot-2");

await data.SaveSessionDataAsync(state);

// Switch profile
await data.UseProfileAsync("bob");
var bobsWallet = await data.LoadProfileDataAsync<WalletSave>("wallet");

// List profiles / sessions
var profiles = await data.GetProfilesAsync();
var sessions = await data.GetSessionsAsync(); // for current profile

// Delete a profile (cleans up all profile data + sessions automatically)
await data.DeleteProfileAsync("alice");
```

## Remembering the last-used profile

A classic use of DeviceData — survives across runs on this device but never travels:

```csharp
[Serializable]
public class LastProfileSave : ISaveData { public string Id; }

const string LastProfileKey = "last-profile";

// On save:
data.SaveDeviceData(LastProfileKey, new LastProfileSave { Id = "alice" });

// On startup:
if (data.DeviceDataExists(LastProfileKey))
    await data.UseProfileAsync(data.LoadDeviceData<LastProfileSave>(LastProfileKey).Id);
```

## Versioning and migrations

Every save is written with a version number alongside it (in a sidecar key `{key}/__version`). On load, the repository compares the stored version with the current version of your save type and runs migrations if they differ — invisibly to your code.

### Version 1 — your first save type

No ceremony. Implement `ISaveData`, save it, load it:

```csharp
[Serializable]
public class WalletSave : ISaveData
{
    public int Gold;
    public int Gems;
}
```

The system writes version `1` alongside automatically. The current version is derived from the type's structure: no nested `Vn` classes means version 1.

### Version 2 — your first breaking change

You renamed `Gold` to `SoftCurrency` and added `HardCurrency`. Nest the old shape and write one static method:

```csharp
[Serializable]
public class WalletSave : ISaveData    // now implicitly version 2
{
    public int SoftCurrency;
    public int HardCurrency;

    [Serializable]
    public class V1 { public int Gold; public int Gems; }

    public static WalletSave MigrateFrom(V1 old) => new()
    {
        SoftCurrency = old.Gold,
        HardCurrency = 0
    };
}
```

Players with `V1` saves automatically get migrated to the current shape on load. Your loading code is unchanged.

### Version 3 — chained migrations

```csharp
[Serializable]
public class WalletSave : ISaveData    // now version 3
{
    public int SoftCurrency;
    public int HardCurrency;
    public string LastUpdated;

    [Serializable]
    public class V2 { public int SoftCurrency; public int HardCurrency; }

    [Serializable]
    public class V1 { public int Gold; public int Gems; }

    public static WalletSave MigrateFrom(V2 old) => new()
    {
        SoftCurrency = old.SoftCurrency,
        HardCurrency = old.HardCurrency,
        LastUpdated  = "unknown"
    };

    public static V2 MigrateFrom(V1 old) => new()
    {
        SoftCurrency = old.Gold,
        HardCurrency = 0
    };
}
```

A `V1` save walks `V1 → V2 → current` automatically.

### Convention rules (enforced at first load)

- **Outer class is always current.** Nested `Vn` classes are preserved old shapes.
- **Current version = max(`Vn`) + 1.** With `V1` and `V2` nested, current is 3.
- **No gaps allowed.** If `V3` exists, `V1` and `V2` must too.
- **One `MigrateFrom` per `Vn`.** Method signature: `public static [NextVersion] MigrateFrom([Vn] old)`. The return type can be the next `Vn+1`, or the outer (current) class.
- **No `__version` key on disk** → treated as the current version (handy when adding versioning to an existing game).
- **Stored version > current** (rolled-back deploy) → throws.

Errors are descriptive and tell you what to add.

## Sync vs async

Every save/load operation has both a sync and async form. Use async by default for safety (especially with backends that go to disk or network). The sync form is convenient for synchronous startup paths and testing.

Delete is async-only because the backend exposes only `DeleteAsync`.

Note: migrations themselves always run synchronously — they're CPU work, not I/O — even when called from an async path.

## Bring your own data bags

The package has no `SessionData<T>` / `ProfileData<T>` base class. Your saved data is a plain `[Serializable]` C# class that implements `ISaveData` — independent of how or where it's stored.

## Editor browser

`Window → Kodachi → Data Browser` opens a window listing all profiles, their profile data keys, and their session slots. Each entry shows the stored version (e.g. `wallet (v2)`). Click any entry to inspect its serialized value.

DeviceData is not shown — there's no index for it (by design). Inspect device keys directly with the persistence backend if needed.

## Key layout (reference)

```
profiles/index                                        ← ProfileIndex
profiles/{profileId}/profile-data/index               ← ProfileDataIndex
profiles/{profileId}/profile-data/{key}               ← ProfileData bag
profiles/{profileId}/profile-data/{key}/__version     ← int, version of the bag
profiles/{profileId}/sessions/index                   ← SessionIndex
profiles/{profileId}/sessions/{sessionId}             ← SessionData bag
profiles/{profileId}/sessions/{sessionId}/__version   ← int, version of the bag
device/{key}                                          ← DeviceData bag
device/{key}/__version                                ← int, version of the bag
```

Index objects (`ProfileIndex`, `SessionIndex`, `ProfileDataIndex`) are not versioned — they're internal to the package and have stable shapes.

## Dependencies

- `com.kodachigames.persistence` — provides `IPersistenceBackend`.
- `UnityServiceLocator` — for service registration.
