namespace ChaoticCupid.PubSubApp
{
    public record PersonInfo(string Username, string City, int Age, string Phone);

    public class PersonState
    {
        public PersonState(PersonInfo info, string connectionId)
        {
            Info = info;
            ConnectionId = connectionId;
            BlockedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Sync = new object();
        }

        public PersonInfo Info { get; }
        public string ConnectionId { get; }
        public HashSet<string> BlockedUsers { get; }
        public bool PendingLetter { get; set; }
        public object Sync { get; }

        public bool IsBlocked(string username)
        {
            lock (Sync)
            {
                return BlockedUsers.Contains(username);
            }
        }

        public bool IsPending()
        {
            lock (Sync)
            {
                return PendingLetter;
            }
        }

        public void SetPending()
        {
            lock (Sync)
            {
                PendingLetter = true;
            }
        }
    }

    public record LetterPayload(string SenderUsername, string SenderCity, int SenderAge, string SenderPhone, string ResponseMessage);

    public record InitResult(bool Ok, string? Error)
    {
        public static InitResult Success() => new(true, null);

        public static InitResult Fail(string error) => new(false, error);
    }
}
