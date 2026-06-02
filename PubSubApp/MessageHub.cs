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
    public class MessageHub : Hub
    {
        private static readonly ConcurrentDictionary<string, PersonState> _persons = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim _tickLock = new(1, 1);
        private static readonly RNGCryptoServiceProvider _rng = new();
        private static readonly string[] _responseMessages =
        {
            "Radujem se nasem susretu!",
            "Zelim da se upoznamo.",
            "Nisam zainteresovan/a za upoznavanje."
        };

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var toRemove = _persons
                .Where(kvp => string.Equals(kvp.Value.ConnectionId, Context.ConnectionId, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var username in toRemove)
            {
                _persons.TryRemove(username, out _);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public Task<InitResult> InitSinglePerson(string username, string city, int age, string phone)
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
            var state = new PersonState(info, Context.ConnectionId);

            if (!_persons.TryAdd(username, state))
            {
                return Task.FromResult(InitResult.Fail("Korisnicko ime vec postoji."));
            }

            Console.WriteLine($"[SERVER] {username} se prijavio/la.");
            return Task.FromResult(InitResult.Success());
        }

        public Task BlockUser(string blockedUsername)
        {
            if (string.IsNullOrWhiteSpace(blockedUsername))
            {
                return Task.CompletedTask;
            }

            if (_persons.TryGetValue(GetCurrentUsername(), out var state))
            {
                lock (state.Sync)
                {
                    state.BlockedUsers.Add(blockedUsername);
                }

                Console.WriteLine($"[SERVER] {state.Info.Username} blokirao/la {blockedUsername}.");
            }

            return Task.CompletedTask;
        }

        public Task ConfirmReceived()
        {
            if (_persons.TryGetValue(GetCurrentUsername(), out var state))
            {
                lock (state.Sync)
                {
                    state.PendingLetter = false;
                }
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
                    await Clients.Client(receiver.ConnectionId).SendAsync("LetterArrived", payload);

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

        private static int GetRandomScore()
        {
            var bytes = new byte[4];
            lock (_rng)
            {
                _rng.GetBytes(bytes);
            }

            var value = BitConverter.ToUInt32(bytes, 0);
            return (int)(value % 101);
        }

        private static string GetRandomResponse()
        {
            var index = GetRandomScore() % _responseMessages.Length;
            return _responseMessages[index];
        }

        private string GetCurrentUsername()
        {
            var entry = _persons.FirstOrDefault(kvp => string.Equals(kvp.Value.ConnectionId, Context.ConnectionId, StringComparison.OrdinalIgnoreCase));
            return entry.Key ?? string.Empty;
        }
    }
}
