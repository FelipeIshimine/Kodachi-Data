using UnityEngine.UIElements;

namespace KodachiGames.Data.Editor
{
    public interface IDataBrowserPreview
    {
        bool CanPreview(byte[] bytes);
        VisualElement CreatePreview(string key, byte[] bytes);
    }
}
