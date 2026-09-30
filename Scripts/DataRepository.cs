using System.Threading;
using KodachiGames.Persistence;
using UnityEngine;

namespace KodachiGames.Data
{
    public class DataRepository
    {
        const string VersionSuffix = "/__version";

        readonly IPersistenceBackend _backend;
        readonly ISaveFormat _format;
        readonly DataContext _context;

        public DataContext Context => _context;
        public ISaveFormat Format => _format;

        public DataRepository(IPersistenceBackend backend, ISaveFormat format, DataContext context)
        {
            _backend = backend ?? throw new System.ArgumentNullException(nameof(backend));
            _format = format ?? throw new System.ArgumentNullException(nameof(format));
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
        }

        // --- Profiles ---

        public async Awaitable<ProfileIndex> GetProfilesAsync(CancellationToken ct = default)
        {
            if (!await _backend.ExistsAsync(_context.ProfileIndexKey, ct))
                return new ProfileIndex();
            return await ReadAsync<ProfileIndex>(_context.ProfileIndexKey, ct);
        }

        public async Awaitable CreateProfileAsync(string profileId, CancellationToken ct = default)
        {
            var index = await GetProfilesAsync(ct);
            if (index.Contains(profileId)) return;
            index.Add(profileId);
            await WriteAsync(_context.ProfileIndexKey, index, ct);
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
            await WriteAsync(_context.ProfileIndexKey, profileIndex, ct);
        }

        // --- Sessions ---

        public async Awaitable<SessionIndex> GetSessionsAsync(CancellationToken ct = default)
        {
            if (!await _backend.ExistsAsync(_context.SessionIndexKey, ct))
                return new SessionIndex();
            return await ReadAsync<SessionIndex>(_context.SessionIndexKey, ct);
        }

        public async Awaitable CreateSessionAsync(string sessionId, CancellationToken ct = default)
        {
            var index = await GetSessionsAsync(ct);
            if (index.Contains(sessionId)) return;
            index.Add(sessionId);
            await WriteAsync(_context.SessionIndexKey, index, ct);
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
            await WriteAsync(_context.SessionIndexKey, index, ct);
        }

        // --- ProfileData ---

        public async Awaitable SaveProfileDataAsync<T>(string key, T data, CancellationToken ct = default) where T : ISaveData
        {
            await EnsureActiveProfileTrackedAsync(ct);
            await TrackProfileDataKeyAsync(key, ct);
            await WriteVersionedAsync(_context.ProfileDataKey(key), data, ct);
        }

        public async Awaitable<T> LoadProfileDataAsync<T>(string key, CancellationToken ct = default) where T : ISaveData
            => await ReadVersionedAsync<T>(_context.ProfileDataKey(key), ct);

        public async Awaitable<bool> ProfileDataExistsAsync(string key, CancellationToken ct = default)
            => await _backend.ExistsAsync(_context.ProfileDataKey(key), ct);

        public async Awaitable SaveProfileBytesAsync(string key, byte[] data, CancellationToken ct = default)
        {
            await EnsureActiveProfileTrackedAsync(ct);
            await TrackProfileDataKeyAsync(key, ct);
            await _backend.WriteAsync(_context.ProfileDataKey(key), data, ct);
        }

        public async Awaitable<byte[]> LoadProfileBytesAsync(string key, CancellationToken ct = default)
            => await _backend.ReadAsync(_context.ProfileDataKey(key), ct);

        public async Awaitable DeleteProfileDataAsync(string key, CancellationToken ct = default)
        {
            await _backend.DeleteAsync(_context.ProfileDataKey(key), ct);
            await _backend.DeleteAsync(_context.ProfileDataKey(key) + VersionSuffix, ct);
            var index = await GetProfileDataIndexAsync(ct);
            if (!index.Contains(key)) return;
            index.Remove(key);
            await WriteAsync(_context.ProfileDataIndexKey, index, ct);
        }

        // --- SessionData ---

        public async Awaitable SaveSessionDataAsync<T>(T data, CancellationToken ct = default) where T : ISaveData
        {
            await EnsureActiveProfileTrackedAsync(ct);
            await TrackSessionIdAsync(_context.SessionId, ct);
            await WriteVersionedAsync(_context.SessionDataKey(_context.SessionId), data, ct);
        }

        public async Awaitable<T> LoadSessionDataAsync<T>(CancellationToken ct = default) where T : ISaveData
            => await ReadVersionedAsync<T>(_context.SessionDataKey(_context.SessionId), ct);

        public async Awaitable<bool> SessionDataExistsAsync(CancellationToken ct = default)
            => await _backend.ExistsAsync(_context.SessionDataKey(_context.SessionId), ct);

        // --- DeviceData ---

        public async Awaitable SaveDeviceDataAsync<T>(string key, T data, CancellationToken ct = default) where T : ISaveData
            => await WriteVersionedAsync(_context.DeviceKey(key), data, ct);

        public async Awaitable<T> LoadDeviceDataAsync<T>(string key, CancellationToken ct = default) where T : ISaveData
            => await ReadVersionedAsync<T>(_context.DeviceKey(key), ct);

        public async Awaitable<bool> DeviceDataExistsAsync(string key, CancellationToken ct = default)
            => await _backend.ExistsAsync(_context.DeviceKey(key), ct);

        public async Awaitable DeleteDeviceDataAsync(string key, CancellationToken ct = default)
        {
            await _backend.DeleteAsync(_context.DeviceKey(key), ct);
            await _backend.DeleteAsync(_context.DeviceKey(key) + VersionSuffix, ct);
        }

        // --- Versioned read/write (the magic) ---

        async Awaitable WriteVersionedAsync<T>(string key, T data, CancellationToken ct)
        {
            var info = MigrationRegistry.Get(typeof(T));
            await WriteAsync(key, data, ct);
            await WriteAsync(key + VersionSuffix, new VersionEnvelope { Version = info.CurrentVersion }, ct);
        }

        async Awaitable<T> ReadVersionedAsync<T>(string key, CancellationToken ct)
        {
            var info = MigrationRegistry.Get(typeof(T));
            var versionKey = key + VersionSuffix;

            try
            {
                if (!await _backend.ExistsAsync(versionKey, ct))
                    return await ReadAsync<T>(key, ct);

                var storedVersion = (await ReadAsync<VersionEnvelope>(versionKey, ct)).Version;

                if (storedVersion == info.CurrentVersion)
                    return await ReadAsync<T>(key, ct);

                return (T)info.Migrate(_format.Deserialize(await _backend.ReadAsync(key, ct), info.TypeOf(storedVersion)), storedVersion);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning(
                    $"[DataRepository] '{key}' could not be deserialized ({e.Message}). The record is " +
                    "corrupt or predates the JSON-serializer migration; discarding it and starting fresh.");
                await _backend.DeleteAsync(key, ct);
                await _backend.DeleteAsync(versionKey, ct);
                return default;
            }
        }

        async Awaitable<T> ReadAsync<T>(string key, CancellationToken ct)
            => (T)_format.Deserialize(await _backend.ReadAsync(key, ct), typeof(T));

        async Awaitable WriteAsync<T>(string key, T data, CancellationToken ct)
            => await _backend.WriteAsync(key, _format.Serialize(data), ct);

        [System.Serializable]
        private class VersionEnvelope
        {
            public int Version;
        }

        // --- Index tracking (internal) ---

        async Awaitable EnsureActiveProfileTrackedAsync(CancellationToken ct)
        {
            var index = await GetProfilesAsync(ct);
            if (index.Contains(_context.ProfileId)) return;
            index.Add(_context.ProfileId);
            await WriteAsync(_context.ProfileIndexKey, index, ct);
        }

        public async Awaitable<ProfileDataIndex> GetProfileDataIndexAsync(CancellationToken ct = default)
        {
            if (!await _backend.ExistsAsync(_context.ProfileDataIndexKey, ct))
                return new ProfileDataIndex();
            return await ReadAsync<ProfileDataIndex>(_context.ProfileDataIndexKey, ct);
        }

        async Awaitable TrackProfileDataKeyAsync(string key, CancellationToken ct)
        {
            var index = await GetProfileDataIndexAsync(ct);
            if (index.Contains(key)) return;
            index.Add(key);
            await WriteAsync(_context.ProfileDataIndexKey, index, ct);
        }

        async Awaitable TrackSessionIdAsync(string sessionId, CancellationToken ct)
        {
            var index = await GetSessionsAsync(ct);
            if (index.Contains(sessionId)) return;
            index.Add(sessionId);
            await WriteAsync(_context.SessionIndexKey, index, ct);
        }
    }
}
