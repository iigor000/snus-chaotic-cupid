using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace ChaoticCupid.PubSubApp
{
    public class MessageHub : Hub
    {
        private readonly IPersonService _personService;
        private readonly ICupidService _cupidService;

        public MessageHub(IPersonService personService, ICupidService cupidService)
        {
            _personService = personService;
            _cupidService = cupidService;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _personService.OnPersonDisconnected(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<InitResult> InitSinglePerson(string username, string city, int age, string phone)
        {
            return await _personService.InitSinglePerson(username, city, age, phone, Context.ConnectionId);
        }

        public async Task<BlockResult> BlockUser(string blockedUsername)
        {
            var username = _personService.GetUsernameByConnectionId(Context.ConnectionId);
            if (string.IsNullOrEmpty(username))
            {
                return BlockResult.Fail("Korisnik nije prijavljen.");
            }

            return await _personService.BlockUser(username, blockedUsername);
        }

        public async Task ConfirmReceived()
        {
            var username = _personService.GetUsernameByConnectionId(Context.ConnectionId);
            if (!string.IsNullOrEmpty(username))
            {
                await _personService.ConfirmReceived(username);
            }
        }

        public async Task CupidonTick()
        {
            await _cupidService.CupidonTick();
        }
    }
}
