using UnityEditor;

namespace Devolfer.Soma
{
    [CustomEditor(typeof(Soma))]
    public class SomaEditor : SomaEditorBase
    {
        protected override string BannerName => "banner-default-1800-200";
    }
}