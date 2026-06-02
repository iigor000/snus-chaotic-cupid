using System.Threading.Tasks;

namespace ChaoticCupid.PubSubApp
{
    public interface IPersonService
    {
        Task<InitResult> InitSinglePerson(string username, string city, int age, string phone, string connectionId);
        Task<BlockResult> BlockUser(string username, string blockedUsername);
        Task ConfirmReceived(string username);
        Task OnPersonDisconnected(string connectionId);
        string? GetUsernameByConnectionId(string connectionId);
    }
}
