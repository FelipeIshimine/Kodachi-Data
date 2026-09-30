using System;
using System.Text;
using UnityEngine;

namespace KodachiGames.Data
{
    [SelectorName("Formats/JSON (JsonUtility)")]
    [Serializable]
    public class JsonSaveFormat : ISaveFormat
    {
        public byte[] Serialize(object data) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));

        public object Deserialize(byte[] bytes, Type type) =>
            JsonUtility.FromJson(Encoding.UTF8.GetString(bytes), type);
    }
}
