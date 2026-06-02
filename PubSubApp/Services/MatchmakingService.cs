using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ChaoticCupid.PubSubApp
{
    public class MatchmakingService : IPersonService, ICupidService
    {
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly ConcurrentDictionary<string, PersonState> _persons = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _tickLock = new(1, 1);
        private readonly RNGCryptoServiceProvider _rng = new();
        private readonly string[] _responseMessages =
        {
            "Radujem se nasem susretu!",
            "Zelim da se upoznamo.",
            "Nisam zainteresovan/a za upoznavanje."
        };

        public MatchmakingService(IHubContext<MessageHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public string? GetUsernameByConnectionId(string connectionId)
        {
            var entry = _persons.FirstOrDefault(kvp => string.Equals(kvp.Value.ConnectionId, connectionId, StringComparison.OrdinalIgnoreCase));
            return entry.Key;
        }

        public Task<InitResult> InitSinglePerson(string username, string city, int age, string phone, string connectionId)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return Task.FromResult(InitResult.Fail("Username ne sme biti prazan."));
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                return Task.FromResult(InitResult.Fail("Grad ne sme biti prazan."));
            }

            if (age <= 0)
            {
                return Task.FromResult(InitResult.Fail("Godine moraju biti pozitivan broj."));
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                return Task.FromResult(InitResult.Fail("Telefon ne sme biti prazan."));
            }

            var info = new PersonInfo(username, city, age, phone);
            var state = new PersonState(info, connectionId);

            if (!_persons.TryAdd(username, state))
            {
                return Task.FromResult(InitResult.Fail("Korisnicko ime vec postoji."));
            }

            Console.WriteLine($"[SERVER] {username} se prijavio/la.");
            return Task.FromResult(InitResult.Success());
        }

        public Task<BlockResult> BlockUser(string username, string blockedUsername)
        {
            if (string.IsNullOrWhiteSpace(blockedUsername))
            {
                return Task.FromResult(BlockResult.Fail("Morate uneti username za blokiranje."));
            }

            if (string.Equals(username, blockedUsername, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(BlockResult.Fail("Ne mozete blokirati samog sebe."));
            }

            if (!_persons.ContainsKey(blockedUsername))
            {
                return Task.FromResult(BlockResult.Fail($"Korisnik {blockedUsername} ne postoji."));
            }

            if (_persons.TryGetValue(username, out var state))
            {
                lock (state.Sync)
                {
                    if (!state.BlockedUsers.Add(blockedUsername))
                    {
                        return Task.FromResult(BlockResult.Fail($"Korisnik {blockedUsername} je vec blokiran."));
                    }
                }

                Console.WriteLine($"[SERVER] {state.Info.Username} blokirao/la {blockedUsername}.");
                return Task.FromResult(BlockResult.Success());
            }

            return Task.FromResult(BlockResult.Fail("Korisnik nije pronadjen."));
        }

        public Task ConfirmReceived(string username)
        {
            if (_persons.TryGetValue(username, out var state))
            {
                lock (state.Sync)
                {
                    state.PendingLetter = false;
                }
            }

            return Task.CompletedTask;
        }

        public Task OnPersonDisconnected(string connectionId)
        {
            var toRemove = _persons
                .Where(kvp => string.Equals(kvp.Value.ConnectionId, connectionId, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var username in toRemove)
            {
                _persons.TryRemove(username, out _);
                Console.WriteLine($"[SERVER] {username} se odjavio/la (diskonektovan/a).");
            }

            return Task.CompletedTask;
        }

        public async Task CupidonTick()
        {
            if (!await _tickLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                var personsSnapshot = _persons.Values.ToList();
                if (personsSnapshot.Count < 2)
                {
                    return;
                }

                foreach (var receiver in personsSnapshot)
                {
                    if (receiver.IsPending())
                    {
                        continue;
                    }

                    var best = FindBestMatch(receiver, personsSnapshot);
                    if (best == null)
                    {
                        continue;
                    }

                    receiver.SetPending();

                    var payload = new LetterPayload(best.Info.Username, best.Info.City, best.Info.Age, best.Info.Phone, GetRandomResponse());
                    await _hubContext.Clients.Client(receiver.ConnectionId).SendAsync("LetterArrived", payload);

                    Console.WriteLine($"[SERVER] Poslato pismo {best.Info.Username} -> {receiver.Info.Username}.");
                }
            }
            finally
            {
                _tickLock.Release();
            }
        }

        private PersonState? FindBestMatch(PersonState receiver, List<PersonState> persons)
        {
            var bestScore = int.MinValue;
            PersonState? best = null;

            foreach (var candidate in persons)
            {
                if (string.Equals(candidate.Info.Username, receiver.Info.Username, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (receiver.IsBlocked(candidate.Info.Username))
                {
                    continue;
                }

                var score = 0;

                if (string.Equals(candidate.Info.City, receiver.Info.City, StringComparison.OrdinalIgnoreCase))
                {
                    score += 30;
                }

                if (Math.Abs(candidate.Info.Age - receiver.Info.Age) <= 2)
                {
                    score += 20;
                }

                score += GetRandomScore();

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private int GetRandomScore()
        {
            var bytes = new byte[4];
            lock (_rng)
            {
                _rng.GetBytes(bytes);
            }

            var value = BitConverter.ToUInt32(bytes, 0);
            return (int)(value % 101);
        }

        private string GetRandomResponse()
        {
            var index = GetRandomScore() % _responseMessages.Length;
            return _responseMessages[index];
        }
    }
}
