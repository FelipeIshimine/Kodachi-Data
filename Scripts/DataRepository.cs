using System.Threading;
using KodachiGames.Persistence;
using UnityEngine;

namespace KodachiGames.Data
{
    public class DataRepository
    {
        const string VersionSuffix = "/__version";

        readonly IPersistenceBackend _backend;
        readonly DataContext _context;

        public DataContext Context => _context;

        public DataRepository(IPersistenceBackend backend, DataContext context)
        {
            _backend = backend;
            _context = context;
        }

        // --- Profiles ---

        public ProfileIndex GetProfiles()
        {
            if (!_backend.Exists(_context.ProfileIndexKey))
                return new ProfileIndex();
            return _backend.Load<ProfileIndex>(_context.ProfileIndexKey);
        }

        public async Awaitable<ProfileIndex> GetProfilesAsync(CancellationToken ct = default)
        {
            if (!_backend.Exists(_context.ProfileIndexKey))
                return new ProfileIndex();
            return await _backend.LoadAsync<ProfileIndex>(_context.ProfileIndexKey, ct);
        }

        public void CreateProfile(string profileId)
        {
            var index = GetProfiles();
            if (index.Contains(profileId)) return;
            index.Add(profileId);
            _backend.Save(_context.ProfileIndexKey, index);
        }

        public async Awaitable CreateProfileAsync(string profileId, CancellationToken ct = default)
        {
            var index = await GetProfilesAsync(ct);
            if (index.Contains(profileId)) return;
            index.Add(profileId);
            await _backend.SaveAsync(_context.ProfileIndexKey, index, ct);
        }

        public void UseProfile(string profileId)
        {
            CreateProfile(profileId);
            _context.SetProfile(profileId);
        }

        public async Awaitable UseProfileAsync(string profileId, CancellationToken ct = default)
        {
            await CreateProfileAsync(profileId, ct);
            _context.SetProfile(profileId);
        }

        public void SetActiveProfile(string profileId) => _context.SetProfile(profileId);

        public async Awaitable DeleteProfileAsync(string profileId, CancellationToken ct = default)
        {
            var previous = _context.ProfileId;
            _context.SetProfile(profileId);

            var profileDataIndex = await GetProfileDataIndexAsync(ct);
            foreach (var key in profileDataIndex.Keys)
            {
                await _backend.DeleteAsync(_context.ProfileDataKey(key), ct);
                await _backend.DeleteAsync(_context.ProfileDataKey(key) + VersionSuffix, ct);
            }
            await _backend.DeleteAsync(_context.ProfileDataIndexKey, ct);

            var sessionIndex = await GetSessionsAsync(ct);
            foreach (var sessionId in sessionIndex.SessionIds)
            {
                await _backend.DeleteAsync(_context.SessionDataKey(sessionId), ct);
                await _backend.DeleteAsync(_context.SessionDataKey(sessionId) + VersionSuffix, ct);
            }
            await _backend.DeleteAsync(_context.SessionIndexKey, ct);

            _context.SetProfile(previous);

            var profileIndex = await GetProfilesAsync(ct);
            profileIndex.Remove(profileId);
            await _backend.SaveAsync(_context.ProfileIndexKey, profileIndex, ct);
        }

        // --- Sessions ---

        public SessionIndex GetSessions()
        {
            if (!_backend.Exists(_context.SessionIndexKey))
                return new SessionIndex();
            return _backend.Load<SessionIndex>(_context.SessionIndexKey);
        }

        public async Awaitable<SessionIndex> GetSessionsAsync(CancellationToken ct = default)
        {
            if (!_backend.Exists(_context.SessionIndexKey))
                return new SessionIndex();
            return await _backend.LoadAsync<SessionIndex>(_context.SessionIndexKey, ct);
        }

        public void CreateSession(string sessionId)
        {
            var index = GetSessions();
            if (index.Contains(sessionId)) return;
            index.Add(sessionId);
            _backend.Save(_context.SessionIndexKey, index);
        }

        public async Awaitable CreateSessionAsync(string sessionId, CancellationToken ct = default)
        {
            var index = await GetSessionsAsync(ct);
            if (index.Contains(sessionId)) return;
            index.Add(sessionId);
            await _backend.SaveAsync(_context.SessionIndexKey, index, ct);
        }

        public void UseSession(string sessionId)
        {
            CreateSession(sessionId);
            _context.SetSession(sessionId);
        }

        public async Awaitable UseSessionAsync(string sessionId, CancellationToken ct = default)
        {
            await CreateSessionAsync(sessionId, ct);
            _context.SetSession(sessionId);
        }

        public void SetActiveSession(string sessionId) => _context.SetSession(sessionId);
        public void ClearActiveSession() => _context.ClearSession();

        public async Awaitable DeleteSessionAsync(string sessionId, CancellationToken ct = default)
        {
            await _backend.DeleteAsync(_context.SessionDataKey(sessionId), ct);
            await _backend.DeleteAsync(_context.SessionDataKey(sessionId) + VersionSuffix, ct);

            var index = await GetSessionsAsync(ct);
            index.Remove(sessionId);
            await _backend.SaveAsync(_context.SessionIndexKey, index, ct);
        }

        // --- ProfileData ---

        public void SaveProfileData<T>(string key, T data) where T : ISaveData
        {
            TrackProfileDataKey(key);
            WriteVersioned(_context.ProfileDataKey(key), data);
        }

        public async Awaitable SaveProfileDataAsync<T>(string key, T data, CancellationToken ct = default) where T : ISaveData
        {
            await TrackProfileDataKeyAsync(key, ct);
            await WriteVersionedAsync(_context.ProfileDataKey(key), data, ct);
        }

        public T LoadProfileData<T>(string key) where T : ISaveData
            => ReadVersioned<T>(_context.ProfileDataKey(key));

        public async Awaitable<T> LoadProfileDataAsync<T>(string key, CancellationToken ct = default) where T : ISaveData
            => await ReadVersionedAsync<T>(_context.ProfileDataKey(key), ct);

        public bool ProfileDataExists(string key) => _backend.Exists(_context.ProfileDataKey(key));

        // --- SessionData ---

        public void SaveSessionData<T>(T data) where T : ISaveData
        {
            TrackSessionId(_context.SessionId);
            WriteVersioned(_context.SessionDataKey(_context.SessionId), data);
        }

        public async Awaitable SaveSessionDataAsync<T>(T data, CancellationToken ct = default) where T : ISaveData
        {
            await TrackSessionIdAsync(_context.SessionId, ct);
            await WriteVersionedAsync(_context.SessionDataKey(_context.SessionId), data, ct);
        }

        public T LoadSessionData<T>() where T : ISaveData
            => ReadVersioned<T>(_context.SessionDataKey(_context.SessionId));

        public async Awaitable<T> LoadSessionDataAsync<T>(CancellationToken ct = default) where T : ISaveData
            => await ReadVersionedAsync<T>(_context.SessionDataKey(_context.SessionId), ct);

        public bool SessionDataExists() => _backend.Exists(_context.SessionDataKey(_context.SessionId));

        // --- DeviceData ---

        public void SaveDeviceData<T>(string key, T data) where T : ISaveData
            => WriteVersioned(_context.DeviceKey(key), data);

        public async Awaitable SaveDeviceDataAsync<T>(string key, T data, CancellationToken ct = default) where T : ISaveData
            => await WriteVersionedAsync(_context.DeviceKey(key), data, ct);

        public T LoadDeviceData<T>(string key) where T : ISaveData
            => ReadVersioned<T>(_context.DeviceKey(key));

        public async Awaitable<T> LoadDeviceDataAsync<T>(string key, CancellationToken ct = default) where T : ISaveData
            => await ReadVersionedAsync<T>(_context.DeviceKey(key), ct);

        public bool DeviceDataExists(string key) => _backend.Exists(_context.DeviceKey(key));

        public async Awaitable DeleteDeviceDataAsync(string key, CancellationToken ct = default)
        {
            await _backend.DeleteAsync(_context.DeviceKey(key), ct);
            await _backend.DeleteAsync(_context.DeviceKey(key) + VersionSuffix, ct);
        }

        // --- Versioned read/write (the magic) ---

        void WriteVersioned<T>(string key, T data)
        {
            var info = MigrationRegistry.Get(typeof(T));
            _backend.Save(key, data);
            _backend.Save(key + VersionSuffix, info.CurrentVersion);
        }

        async Awaitable WriteVersionedAsync<T>(string key, T data, CancellationToken ct)
        {
            var info = MigrationRegistry.Get(typeof(T));
            await _backend.SaveAsync(key, data, ct);
            await _backend.SaveAsync(key + VersionSuffix, info.CurrentVersion, ct);
        }

        T ReadVersioned<T>(string key)
        {
            var info = MigrationRegistry.Get(typeof(T));
            var versionKey = key + VersionSuffix;

            // No version stored → pre-versioning save, treat as current
            if (!_backend.Exists(versionKey))
                return _backend.Load<T>(key);

            var storedVersion = _backend.Load<int>(versionKey);

            if (storedVersion == info.CurrentVersion)
                return _backend.Load<T>(key);

            // Migrations run sync regardless of caller — they are CPU work, not I/O
            return (T)info.LoadAndMigrate(_backend, key, storedVersion);
        }

        async Awaitable<T> ReadVersionedAsync<T>(string key, CancellationToken ct)
        {
            var info = MigrationRegistry.Get(typeof(T));
            var versionKey = key + VersionSuffix;

            if (!_backend.Exists(versionKey))
                return await _backend.LoadAsync<T>(key, ct);

            var storedVersion = await _backend.LoadAsync<int>(versionKey, ct);

            if (storedVersion == info.CurrentVersion)
                return await _backend.LoadAsync<T>(key, ct);

            return (T)info.LoadAndMigrate(_backend, key, storedVersion);
        }

        // --- Index tracking (internal) ---

        ProfileDataIndex GetProfileDataIndex()
        {
            if (!_backend.Exists(_context.ProfileDataIndexKey))
                return new ProfileDataIndex();
            return _backend.Load<ProfileDataIndex>(_context.ProfileDataIndexKey);
        }

        async Awaitable<ProfileDataIndex> GetProfileDataIndexAsync(CancellationToken ct = default)
        {
            if (!_backend.Exists(_context.ProfileDataIndexKey))
                return new ProfileDataIndex();
            return await _backend.LoadAsync<ProfileDataIndex>(_context.ProfileDataIndexKey, ct);
        }

        void TrackProfileDataKey(string key)
        {
            var index = GetProfileDataIndex();
            if (index.Contains(key)) return;
            index.Add(key);
            _backend.Save(_context.ProfileDataIndexKey, index);
        }

        async Awaitable TrackProfileDataKeyAsync(string key, CancellationToken ct)
        {
            var index = await GetProfileDataIndexAsync(ct);
            if (index.Contains(key)) return;
            index.Add(key);
            await _backend.SaveAsync(_context.ProfileDataIndexKey, index, ct);
        }

        void TrackSessionId(string sessionId)
        {
            var index = GetSessions();
            if (index.Contains(sessionId)) return;
            index.Add(sessionId);
            _backend.Save(_context.SessionIndexKey, index);
        }

        async Awaitable TrackSessionIdAsync(string sessionId, CancellationToken ct)
        {
            var index = await GetSessionsAsync(ct);
            if (index.Contains(sessionId)) return;
            index.Add(sessionId);
            await _backend.SaveAsync(_context.SessionIndexKey, index, ct);
        }
    }
}
