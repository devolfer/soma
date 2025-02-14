using UnityEngine;

namespace Devolfer.Soma
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        [Tooltip(
            "Optionally override the gameobjects' hide flags, if you want the gameobject e.g. not to be shown in the hierarchy." +
            "\n\nBe careful with the not saving options, as you will have to manage deleting manually yourself then!")]
        [SerializeField] protected HideFlags _hideFlags = HideFlags.None;

        protected static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance != null) return s_instance;

                s_instance = FindAnyObjectByType<T>();

                return s_instance != null ?
                    s_instance :
                    s_instance = new GameObject($"{typeof(T).Name}").AddComponent<T>();
            }
        }

        protected virtual void Awake()
        {
            if (!Application.isPlaying) return;

            Setup();
        }

        protected virtual void OnDestroy() => s_instance = null;

        protected virtual void Setup()
        {
            gameObject.hideFlags = _hideFlags;

            s_instance = this as T;
        }
    }
}