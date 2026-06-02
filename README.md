# ChaoticCupid 🏹💬

ChaoticCupid is a lightweight, real-time pub/sub matchmaking demo built using **ASP.NET Core SignalR** and **.NET 9.0**. It showcases how a central server can coordinate real-time communication, maintain state for connected users, calculate match affinity, and handle dynamic pub/sub events.

---

## 🏗️ System Architecture

The application is split into three main components:

```
                  +--------------------------------+
                  |         PubSubApp (Server)     |
                  |                                |
                  |  - SignalR Message Hub         |
                  |  - Matchmaking Scoring Engine  |
                  |  - Client State Cache          |
                  +--------------------------------+
                     ^     ^                 |
        CupidonTick  |     | Register/Block  | LetterArrived
            (60s)    |     | Confirm         | (Event payload)
                     |     |                 v
             +-------+--+ +------------------+----+
             | Pub      | | Sub (Subscribers)     |
             | (Cupid)  | | - Alice               |
             |          | | - Bob                 |
             |          | | - Charlie             |
             +----------+ +-----------------------+
```

1. **`PubSubApp` (Server)**:
   - Hosts the SignalR `/messageHub` endpoint.
   - Manages client registrations, connection lifetimes, block lists, and pending letter states in memory.
   - Calculates pairing scores and dispatches matchmaking letters.
2. **`Pub` (Publisher)**:
   - A background console client that acts as the "Cupid's heartbeat".
   - Periodically invokes the matchmaking tick (`CupidonTick`) every 60 seconds on the server.
3. **`Sub` (Subscriber)**:
   - Interactive console clients representing single people seeking matches.
   - Prompts for profile details on startup, connects to the server, and processes incoming matches in real time.

---

## ⚡ Real-Time Protocol & API Contract

The communication is established over WebSocket (or SignalR fallback transports) at `http://localhost:7128/messageHub`.

### Hub Methods (Client to Server)

| Method | Arguments | Returns | Description |
| :--- | :--- | :--- | :--- |
| `InitSinglePerson` | `string username, string city, int age, string phone` | `InitResult` | Registers a subscriber with their profile data. Returns validation status. |
| `BlockUser` | `string blockedUsername` | `Task` | Prevents matchmaking between the caller and the blocked user. |
| `ConfirmReceived` | *(None)* | `Task` | Clears the pending letter flag for the caller, allowing them to receive future matches. |
| `CupidonTick` | *(None)* | `Task` | Triggers a matchmaking pass across all registered and available users. |

### Client-Side Handlers (Server to Client)

| Event | Payload Type | Description |
| :--- | :--- | :--- |
| `LetterArrived` | `LetterPayload` | Sent to a subscriber when a match is successfully calculated. |

### Data Models (`Models.cs`)

- **`PersonInfo`**: `(string Username, string City, int Age, string Phone)`
- **`LetterPayload`**: `(string SenderUsername, string SenderCity, int SenderAge, string SenderPhone, string ResponseMessage)`
- **`InitResult`**: `(bool Ok, string? Error)`

---

## 🎯 Matchmaking Algorithm

When the `CupidonTick` is executed:
1. The server gathers a snapshot of all active users.
2. It iterates through each subscriber who is **not** currently waiting to confirm a pending letter (`PendingLetter == false`).
3. For each eligible receiver, it evaluates all other active users to find the **best match** using a scoring system:
   - **Base Score**: Starts at `0`.
   - **City Affinity**: **+30 points** if the candidate is in the same city as the receiver.
   - **Age Affinity**: **+20 points** if the age difference is within 2 years (`|receiver.Age - candidate.Age| <= 2`).
   - **Random Bonus**: **+0 to 100 points** (calculated securely using a cryptographic random number generator).
   - **Exclusion/Block List**: Candidates who have been blocked by the receiver are skipped entirely.
4. The candidate with the highest score is matched.
5. The receiver's state is set to `PendingLetter = true` (blocking further letters until confirmed).
6. A `LetterArrived` event is sent to the receiver with one of the following randomized response messages:
   - `"Radujem se nasem susretu!"` (I look forward to our meeting!)
   - `"Zelim da se upoznamo."` (I want us to get to know each other.)
   - `"Nisam zainteresovan/a za upoznavanje."` (I'm not interested in getting acquainted.)
7. **Privacy Safe Guard**: If the message is `"Nisam zainteresovan/a za upoznavanje."`, the sender's phone number is redacted and hidden from the subscriber.

---

## 🛠️ Project Structure

```
ChaoticCupid/
├── PubSubApp/               # ASP.NET Core SignalR Server
│   ├── Program.cs           # Host building, SignalR endpoint mapping, Swagger
│   ├── MessageHub.cs        # Matchmaking algorithms, state tracking, and hub endpoints
│   ├── Models.cs            # Shared record types and status objects
│   └── Properties/
│       └── launchSettings.json # Ports and environment settings
├── Pub/                     # Publisher Console Client
│   └── Program.cs           # Connects to hub and calls CupidonTick every minute
├── Sub/                     # Subscriber Console Client
│   └── Program.cs           # Interactive user registration, commands, and receipt logic
└── README.md                # System documentation
```

---

## 🚀 Running the Demo

### Prerequisites
- **.NET SDK 9.0** installed.
- Three active terminal windows or panels.

### Step-by-Step Launch

1. **Start the Server**
   In the first terminal, navigate to the project root and run:
   ```bash
   dotnet run --project PubSubApp
   ```
   *The server will start listening at `http://localhost:7128/messageHub`.*

2. **Start the Subscribers**
   In the second terminal (and optionally third, fourth, etc.), start a subscriber client:
   ```bash
   dotnet run --project Sub
   ```
   *You will be prompted to enter your profile information:*
   ```text
   Unesite username: Alice
   Unesite grad: Belgrade
   Unesite godine: 25
   Unesite broj telefona: 064123456
   ```

3. **Start the Publisher**
   In another terminal, start the publisher to begin matchmaking:
   ```bash
   dotnet run --project Pub
   ```
   *This client will trigger the matchmaking engine immediately and then repeat every 60 seconds.*

---

## 🎮 Subscriber Commands

Once a subscriber is registered, you can use interactive console inputs:

- **`/block <username>`**
  Blocks the specified user. They will no longer be considered as a match candidate for you.
  
- **`Enter` (Pressing Enter on an empty line)**
  When you receive a love letter, your CLI will display `Potvrdite prijem pritiskom Enter...`. Pressing **Enter** sends a confirmation back to the server (`ConfirmReceived`), opening you up to receive new matches in the next tick.
