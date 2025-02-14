using UnityEditor;
using UnityEngine;

namespace Devolfer.Soma
{
    public class MenuItems
    {
        [MenuItem("GameObject/Audio/Soma", priority = 1, secondaryPriority = 0)]
        private static void CreateSoundManager()
        {
            GameObject newGameObject = new("Soma", typeof(Soma));
            Selection.activeGameObject = newGameObject;

            Soma[] managers = Object.FindObjectsByType<Soma>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (managers.Length > 1)
                Debug.Log($"There are more than one {nameof(Soma)} instances in the scene ({managers.Length})");
        }
        
        [MenuItem("GameObject/Audio/Soma Emitter", priority = 1, secondaryPriority = 1)]
        private static void CreateSoundEmitter()
        {
            GameObject newGameObject = new("SomaEmitter", typeof(SomaEmitter));
            Selection.activeGameObject = newGameObject;
        }
        
        [MenuItem("GameObject/Audio/Soma Volume Mixer", priority = 1, secondaryPriority = 2)]
        private static void CreateSoundMixer()
        {
            GameObject newGameObject = new("SomaVolumeMixer", typeof(SomaVolumeMixer));
            Selection.activeGameObject = newGameObject;
        }
    }
}