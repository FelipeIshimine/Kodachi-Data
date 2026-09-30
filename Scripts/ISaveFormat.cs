using System;

namespace KodachiGames.Data
{
    public interface ISaveFormat
    {
        byte[] Serialize(object data);
        object Deserialize(byte[] bytes, Type type);
    }
}
