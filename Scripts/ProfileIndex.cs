using System;
using System.Collections.Generic;

namespace KodachiGames.Data
{
    [Serializable]
    public class ProfileIndex
    {
        public List<string> ProfileIds = new();

        public bool Contains(string profileId) => ProfileIds.Contains(profileId);
        public void Add(string profileId) => ProfileIds.Add(profileId);
        public void Remove(string profileId) => ProfileIds.Remove(profileId);
    }
}
