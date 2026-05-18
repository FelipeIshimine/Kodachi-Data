using System;
using System.Collections.Generic;

namespace KodachiGames.Data
{
    [Serializable]
    public class SessionIndex
    {
        public List<string> SessionIds = new();

        public bool Contains(string sessionId) => SessionIds.Contains(sessionId);
        public void Add(string sessionId) => SessionIds.Add(sessionId);
        public void Remove(string sessionId) => SessionIds.Remove(sessionId);
    }
}
