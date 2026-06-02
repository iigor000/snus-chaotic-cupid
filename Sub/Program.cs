using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChaoticCupid.Sub
{
    internal class Program
    {
        private static readonly object _sync = new();
        private static bool _pendingLetter;

        private static async Task Main()
        {
            var username = ReadNonEmpty("Unesite username: ");
            var city = ReadNonEmpty("Unesite grad: ");
            var age = ReadPositiveInt("Unesite godine: ");
            var phone = ReadPhone("Unesite broj telefona: ");

            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7128/messageHub")
                .Build();

            connection.On<LetterPayload>("LetterArrived", async payload =>
            {
                lock (_sync)
                {
                    _pendingLetter = true;
                }

                var response = payload.ResponseMessage;
                Console.WriteLine("\n[SUB] Stiglo pismo!");
                Console.WriteLine($"Od: {payload.SenderUsername}");
                Console.WriteLine($"Grad: {payload.SenderCity}");
                Console.WriteLine($"Godine: {payload.SenderAge}");

                if (!string.Equals(response, "Nisam zainteresovan/a za upoznavanje.", StringComparison.Ordinal))
                {
                    Console.WriteLine($"Telefon: {payload.SenderPhone}");
                }

                Console.WriteLine($"Poruka: {response}");
                Console.WriteLine("Potvrdite prijem pritiskom Enter...");

                await Task.CompletedTask;
            });

            await connection.StartAsync();

            var initResult = await connection.InvokeAsync<InitResult>("InitSinglePerson", username, city, age, phone);
            if (!initResult.Ok)
            {
                Console.WriteLine($"[SUB] Greska: {initResult.Error}");
                return;
            }

            Console.WriteLine("[SUB] Uspesno ste prijavljeni. Komande: /block username");

            while (true)
            {
                var line = Console.ReadLine();
                if (line == null)
                {
                    continue;
                }

                if (IsPending())
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        await connection.InvokeAsync("ConfirmReceived");
                        SetPending(false);
                        Console.WriteLine("[SUB] Prijem potvrdjen.");
                    }
                    else
                    {
                        Console.WriteLine("[SUB] Potrebno je potvrditi prijem prethodnog pisma.");
                    }

                    continue;
                }

                if (line.StartsWith("/block ", StringComparison.OrdinalIgnoreCase))
                {
                    var blocked = line.Substring("/block ".Length).Trim();
                    if (string.IsNullOrWhiteSpace(blocked))
                    {
                        Console.WriteLine("[SUB] Niste uneli username za blokiranje.");
                        continue;
                    }

                    var result = await connection.InvokeAsync<BlockResult>("BlockUser", blocked);
                    if (result.Ok)
                    {
                        Console.WriteLine($"[SUB] Korisnik {blocked} je uspesno blokiran.");
                    }
                    else
                    {
                        Console.WriteLine($"[SUB] Greska: {result.Error}");
                    }
                    continue;
                }

                Console.WriteLine("[SUB] Nepoznata komanda. Koristite /block username.");
            }
        }

        private static bool IsPending()
        {
            lock (_sync)
            {
                return _pendingLetter;
            }
        }

        private static void SetPending(bool value)
        {
            lock (_sync)
            {
                _pendingLetter = value;
            }
        }

        private static string ReadNonEmpty(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(input))
                {
                    return input.Trim();
                }

                Console.WriteLine("Unos ne sme biti prazan.");
            }
        }

        private static int ReadPositiveInt(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Unos ne sme biti prazan.");
                    continue;
                }

                if (!int.TryParse(input, out var value))
                {
                    Console.WriteLine("Unos mora biti broj.");
                    continue;
                }

                if (value <= 0)
                {
                    Console.WriteLine("Unos mora biti pozitivan broj.");
                    continue;
                }

                return value;
            }
        }

        private static string ReadPhone(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Unos ne sme biti prazan.");
                    continue;
                }

                var trimmed = input.Trim();
                if (!trimmed.All(char.IsDigit))
                {
                    Console.WriteLine("Telefon mora sadrzati samo cifre.");
                    continue;
                }

                if (trimmed.All(c => c == '0'))
                {
                    Console.WriteLine("Telefon mora biti pozitivan broj.");
                    continue;
                }

                return trimmed;
            }
        }
    }

    public record LetterPayload(string SenderUsername, string SenderCity, int SenderAge, string SenderPhone, string ResponseMessage);

    public record InitResult(bool Ok, string? Error);

    public record BlockResult(bool Ok, string? Error);
}
