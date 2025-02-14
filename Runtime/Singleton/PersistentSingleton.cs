using UnityEngine;

namespace Devolfer.Soma
{
    public class PersistentSingleton<T> : Singleton<T> where T : Component
    {
        protected override void Setup()
        {
            transform.SetParent(null);

            if (s_instance != null)
            {
                if (s_instance != this) Destroy(gameObject);
            }
            else
            {
                base.Setup();

                DontDestroyOnLoad(gameObject);
            }
        }
    }
}