using UnityEngine;

namespace Devolfer.Soma
{
    public class PersistentSingleton<T> : MonoBehaviour where T : Component
    {
        internal static bool HasInstance => s_instance != null;
        internal static bool HasSetup;
        internal static bool MarkedForDestruction;

        internal static bool TryGetInstance(out T instance)
        {
            instance = Instance;

            return HasInstance && HasSetup && !MarkedForDestruction;
        } 

        private static T Instance
        {
            get
            {
                if (MarkedForDestruction) return null;
                
                if (HasInstance && HasSetup) return s_instance;

                s_instance = FindAnyObjectByType<T>();

                if (!HasInstance) s_instance = new GameObject($"{typeof(T).Name}").AddComponent<T>();

                HasSetup = DoSetup(s_instance.transform, s_instance.gameObject);
                
                return HasSetup ? s_instance : s_instance = null;
            }
        }

        private static T s_instance;

        protected virtual void Awake() => LazySetup();

        protected virtual void OnDestroy()
        {
            if (s_instance != this) return;
            
            MarkedForDestruction = true;
        }

        protected virtual void LazySetup()
        {
            if (!Application.isPlaying) return;

            if (HasInstance && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (HasSetup) return;
            
            HasSetup = DoSetup(transform, gameObject);
            if (HasSetup) s_instance = this as T;
        }

        private static bool DoSetup(Transform t, GameObject go)
        {
            if (!Application.isPlaying) return false;
            
            t.SetParent(null);
            DontDestroyOnLoad(go);
            
            return true;
        }
    }
}