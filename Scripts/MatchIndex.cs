using System;
using System.Collections.Generic;

namespace KodachiGames.Data
{
    [Serializable]
    public class MatchIndex
    {
        public List<string> MatchIds = new();

        public bool Contains(string matchId) => MatchIds.Contains(matchId);
        public void Add(string matchId) => MatchIds.Add(matchId);
        public void Remove(string matchId) => MatchIds.Remove(matchId);
    }
}
