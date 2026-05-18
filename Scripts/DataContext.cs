namespace KodachiGames.Data
{
    public class DataContext
    {
        public const string DefaultProfileId = "default";
        public const string DefaultSessionId = "default";

        public string ProfileId { get; private set; } = DefaultProfileId;
        public string SessionId { get; private set; } = DefaultSessionId;

        public void SetProfile(string profileId) => ProfileId = profileId;
        public void SetSession(string sessionId) => SessionId = sessionId;
        public void ClearSession() => SessionId = null;

        public string ProfileIndexKey => "profiles/index";
        public string ProfileDataIndexKey => $"profiles/{ProfileId}/profile-data/index";
        public string SessionIndexKey => $"profiles/{ProfileId}/sessions/index";
        public string ProfileDataKey(string key) => $"profiles/{ProfileId}/profile-data/{key}";
        public string SessionDataKey(string sessionId) => $"profiles/{ProfileId}/sessions/{sessionId}";
        public string DeviceKey(string key) => $"device/{key}";
    }
}
