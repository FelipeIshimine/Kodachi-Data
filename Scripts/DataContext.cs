namespace KodachiGames.Data
{
    public class DataContext
    {
        public string ProfileId { get; private set; }
        public string MatchId { get; private set; }

        public void SetProfile(string profileId) => ProfileId = profileId;
        public void SetMatch(string matchId) => MatchId = matchId;
        public void ClearMatch() => MatchId = null;

        // Index keys — profile-independent
        public string ProfileIndexKey => "profiles/index";
        public string MatchIndexKey => $"profiles/{ProfileId}/matches/index";

        // Data keys
        public string MetaKey(string key) => $"profiles/{ProfileId}/meta/{key}";
        public string GameDataKey => $"profiles/{ProfileId}/matches/{MatchId}";
    }
}
