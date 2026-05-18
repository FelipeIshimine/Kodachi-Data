using UnityEngine;
using UnityServiceLocator;
using KodachiGames.Persistence;

namespace KodachiGames.Data
{
    [DefaultExecutionOrder(1)]
    public class DataServiceInstaller : MonoBehaviour
    {
        void Awake()
        {
            ServiceLocator.For(this).Get<IPersistenceBackend>(out var backend);

            var context = new DataContext();
            var repository = new DataRepository(backend, context);

            ServiceLocator.For(this).Register(context);
            ServiceLocator.For(this).Register(repository);
        }

        void OnDestroy()
        {
            ServiceLocator.For(this).Unregister<DataContext>();
            ServiceLocator.For(this).Unregister<DataRepository>();
        }
    }
}
