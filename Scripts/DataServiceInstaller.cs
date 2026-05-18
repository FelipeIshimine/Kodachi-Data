using UnityEngine;
using UnityServiceLocator;

namespace KodachiGames.Data
{
    public class DataServiceInstaller : MonoBehaviour
    {
        void Awake()
        {
            var context = new DataContext();
            ServiceLocator.For(this).Register(context);
        }
    }
}
