using System.IO;
using UnityEditor;
using UnityEngine;

namespace Devolfer.Soma
{
    internal static class InspectorUtility
    {
        private const string BannerPackagePath = "Packages/com.devolfer.soma/Editor/Inspector/Banner/";
        private const string BannerLocalPath = "Assets/soma/Editor/Inspector/Banner/";

        internal static Texture2D GetBanner(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string path = Path.Combine(BannerPackagePath, name + ".png");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture) return texture;
            
            path = Path.Combine(BannerLocalPath, name + ".png");
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            return texture;
        }

        internal static void DrawBanner(Texture2D texture)
        {
            float imageWidth = EditorGUIUtility.currentViewWidth;
            float imageHeight = imageWidth * texture.height / texture.width;
            Rect rect = GUILayoutUtility.GetRect(imageWidth, imageHeight);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }
    }
}