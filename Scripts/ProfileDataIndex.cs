using System;
using System.Collections.Generic;

namespace KodachiGames.Data
{
    [Serializable]
    public class ProfileDataIndex
    {
        public List<string> Keys = new();

        public bool Contains(string key) => Keys.Contains(key);
        public void Add(string key) => Keys.Add(key);
        public void Remove(string key) => Keys.Remove(key);
    }
}
