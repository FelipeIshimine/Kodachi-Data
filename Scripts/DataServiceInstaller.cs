using UnityEngine;
using UnityServiceLocator;
using KodachiGames.Persistence;

namespace KodachiGames.Data
{
    [DefaultExecutionOrder(1)]
    public class DataServiceInstaller : MonoBehaviour
    {
        [Tooltip("Optional. Leave empty when a PersistenceServiceInstaller (or another installer) " +
                 "already registers an IPersistenceBackend — this component will use that one. " +
                 "Assign a backend here to make this a self-contained, single-component setup for " +
                 "Data-only projects (the backend is then registered for everyone else too).")]
        [SerializeReference, TypeSelector]
        private IPersistenceBackend backend;

        private bool _ownsBackend;
        private DataContext _context;
        private DataRepository _repository;

        void Awake()
        {
            var services = ServiceLocator.For(this);

            if (!services.TryGet(out IPersistenceBackend resolved))
            {
                if (backend == null)
                {
                    Debug.LogError(
                        "[DataServiceInstaller] No IPersistenceBackend is registered and none is " +
                        "assigned. Either add a PersistenceServiceInstaller (it runs first) to the " +
                        "scene, or assign a Backend on this component.", this);
                    return;
                }

                resolved = backend;
                services.Register<IPersistenceBackend>(resolved);
                _ownsBackend = true;
            }

            _context = new DataContext();
            _repository = new DataRepository(resolved, _context);

            services.Register(_context);
            services.Register(_repository);
        }

        void OnDestroy()
        {
            var services = ServiceLocator.For(this);

            if (_repository != null) services.Unregister<DataRepository>();
            if (_context != null) services.Unregister<DataContext>();
            if (_ownsBackend) services.Unregister<IPersistenceBackend>(backend);
        }
    }
}
