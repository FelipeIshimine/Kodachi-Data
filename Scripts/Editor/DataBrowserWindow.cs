using UnityEditor;
using UnityEngine.UIElements;

namespace KodachiGames.Data.Editor
{
    public class DataBrowserWindow : EditorWindow
    {
        [MenuItem("Kodachi/Data Browser")]
        public static void Open() => GetWindow<DataBrowserWindow>("Data Browser");

        void CreateGUI()
        {
            rootVisualElement.Add(new Label("Data Browser — coming soon."));
        }
    }
}
