using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KodachiGames.Persistence;

namespace KodachiGames.Data
{
    internal static class MigrationRegistry
    {
        static readonly Dictionary<Type, TypeMigrationInfo> Cache = new();

        internal static TypeMigrationInfo Get(Type type)
        {
            if (Cache.TryGetValue(type, out var cached))
                return cached;

            var info = Discover(type);
            Cache[type] = info;
            return info;
        }

        static TypeMigrationInfo Discover(Type type)
        {
            // --- Find all nested Vn classes ---
            var versionedTypes = new Dictionary<int, Type>();
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (TryParseVersionName(nested.Name, out int v))
                    versionedTypes[v] = nested;
            }

            int currentVersion = versionedTypes.Count == 0 ? 1 : versionedTypes.Keys.Max() + 1;

            // --- Check for gaps in the Vn sequence ---
            for (int v = 1; v < currentVersion; v++)
            {
                if (!versionedTypes.ContainsKey(v))
                    throw new InvalidOperationException(
                        $"{type.Name}: missing V{v} nested class — gap in migration chain. " +
                        $"Expected V1 through V{currentVersion - 1}.");
            }

            // --- Find all MigrateFrom methods, keyed by their parameter type ---
            var migrators = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "MigrateFrom" && m.GetParameters().Length == 1)
                .ToDictionary(m => m.GetParameters()[0].ParameterType);

            // --- Ensure every Vn has a corresponding MigrateFrom ---
            foreach (var (v, vType) in versionedTypes)
            {
                if (!migrators.ContainsKey(vType))
                    throw new InvalidOperationException(
                        $"{type.Name}: no 'public static {type.Name} MigrateFrom({vType.Name} old)' found. " +
                        $"Add a MigrateFrom method to handle migration from version {v}.");
            }

            return new TypeMigrationInfo(type, currentVersion, versionedTypes, migrators);
        }

        static bool TryParseVersionName(string name, out int version)
        {
            version = 0;
            return name.Length > 1 && name[0] == 'V' && int.TryParse(name.Substring(1), out version);
        }
    }

    internal class TypeMigrationInfo
    {
        readonly Type _currentType;
        readonly Dictionary<int, Type> _versionedTypes;
        readonly Dictionary<Type, MethodInfo> _migrators;

        internal int CurrentVersion { get; }

        internal TypeMigrationInfo(
            Type currentType,
            int currentVersion,
            Dictionary<int, Type> versionedTypes,
            Dictionary<Type, MethodInfo> migrators)
        {
            _currentType = currentType;
            CurrentVersion = currentVersion;
            _versionedTypes = versionedTypes;
            _migrators = migrators;
        }

        // Loads the blob as the stored version type, then walks the chain up to current.
        internal object LoadAndMigrate(IPersistenceBackend backend, string key, int storedVersion)
        {
            var storedType = storedVersion == CurrentVersion
                ? _currentType
                : _versionedTypes[storedVersion];

            // Invoke backend.Load<StoredType>(key) via reflection
            var loadMethod = typeof(IPersistenceBackend)
                .GetMethod(nameof(IPersistenceBackend.Load))
                .MakeGenericMethod(storedType);

            var data = loadMethod.Invoke(backend, new object[] { key });

            // Walk the chain: V1 → V2 → ... → current
            for (int v = storedVersion; v < CurrentVersion; v++)
            {
                var migrator = _migrators[data.GetType()];
                data = migrator.Invoke(null, new[] { data });
            }

            return data;
        }
    }
}
