using UnityEditor;
using UnityEngine;

namespace Devolfer.Soma
{
    public abstract class SomaEditorBase : Editor
    {
        protected abstract string BannerName { get; }
        protected Texture2D _bannerTexture;

        protected virtual void OnEnable()
        {
            _bannerTexture = InspectorUtility.GetBanner(BannerName);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_bannerTexture)
            {
                InspectorUtility.DrawBanner(_bannerTexture);

                DrawPropertiesExcluding(serializedObject, "m_Script");
            }
            else
            {
                DrawDefaultInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}