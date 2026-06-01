# ChaoticCupid

A simple pub/sub matchmaking demo built with ASP.NET Core SignalR. One server (PubSubApp) maintains connected users, a publisher (Pub) triggers periodic matchmaking, and multiple subscribers (Sub) receive "love letters" with a randomized response.

## Components

- PubSubApp (server)
  - ASP.NET Core app hosting a SignalR hub at /messageHub.
  - Tracks connected users, blocked users, and pending letters.
  - Produces a randomized response message for each match.
- Pub (publisher)
  - Console app that calls the hub method CupidonTick every 60 seconds.
- Sub (subscriber)
  - Console app that registers a user and listens for LetterArrived events.
  - Supports blocking by username and confirms letter receipt.

## How it works

1. Each subscriber calls InitSinglePerson with profile data (username, city, age, phone).
2. The publisher calls CupidonTick periodically.
3. For every subscriber without a pending letter, the server picks the best match:
   - +30 points if the city matches.
   - +20 points if the age difference is within 2 years.
   - +0..100 random bonus.
   - Blocked users are excluded.
4. The server sends a LetterArrived event to the receiver with the sender data and a random response message.
5. The subscriber must press Enter to confirm delivery; until confirmed, no new letters are sent to that subscriber.

## Requirements

- .NET SDK 9.0
- Linux, macOS, or Windows

## Run the demo

Open three terminals in the repository root and run:

1) Start the server

```bash
dotnet run --project PubSubApp
```

2) Start one or more subscribers

```bash
dotnet run --project Sub
```

3) Start the publisher

```bash
dotnet run --project Pub
```

The server listens on http://localhost:7128/messageHub (from PubSubApp/Properties/launchSettings.json).

## Subscriber commands

- /block username
  - Blocks a user from being matched with you.

When a letter arrives, press Enter to confirm receipt.

## Project layout

- PubSubApp/
  - Program.cs: ASP.NET Core setup and SignalR hub endpoint.
  - MessageHub.cs: matchmaking and message dispatch logic.
  - Models.cs: shared record types for the hub.
- Pub/
  - Program.cs: periodic trigger for CupidonTick.
- Sub/
  - Program.cs: user registration, message handler, and commands.

## Notes

- Usernames are unique and case-insensitive on the server.
- Only one matchmaking tick runs at a time (guarded by a semaphore).
- Letters are sent only if the receiver does not have a pending letter.
