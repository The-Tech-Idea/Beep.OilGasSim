# Beep Oil and Gas Sim — local run guide

## Three separate pieces (by design)

```text
Browser (React)  →  API (ASP.NET Core)  →  Session store (memory / SQLite / DB)
   :5173                  :5080
```

- **Web client** — UI only. Talks to the API over HTTP/WebSocket.
- **API server** — game rules, turns, multiplayer, AI. This is what you run with `dotnet run`.
- **Session store** — where active games are saved. **Not the same as the API process.**

You do **not** need PostgreSQL, SQL Server, or any database server to play locally.

## Quick start (no database install)

**Terminal 1 — API**

```powershell
cd Game
dotnet run --project src/Beep.OilGasSim.Api
```

**Terminal 2 — Web client**

```powershell
cd Game\client\beep-oil-gas-sim-web
npm run dev
```

Open http://localhost:5173/

Check API: http://localhost:5080/health

## Persistence options

| Provider | Needs install? | Games survive API restart? |
|----------|----------------|----------------------------|
| **InMemory** | No | No |
| **SQLite** (default in Development) | No — single `.db` file | Yes |
| **SQL Server** | Yes (LocalDB / Express) | Planned |
| **PostgreSQL** | Yes (Docker or server) | Planned |

Configure in `src/Beep.OilGasSim.Api/appsettings.json` or `appsettings.Development.json`:

```json
{
  "Persistence": {
    "Provider": "Sqlite",
    "SqlitePath": "data/beepoilgas.db"
  }
}
```

### Setup wizard

From the `Game` folder:

```powershell
dotnet run --project src/Beep.OilGasSim.Api -- setup
```

Writes `appsettings.Development.local.json` with your choice (InMemory, SQLite, SQL Server, or PostgreSQL).

## Why PostgreSQL was in Docker

The `docker-compose.yml` Postgres service is for **future production-style deployment**, not required for local dev. The API does not connect to it yet unless you configure a provider and we finish SQL Server/PostgreSQL stores.

## Troubleshooting

- **500 on lobby** — start the API first; restart after pulling fixes.
- **`npm run dev` Bun error** — use `.\dev.cmd` in the client folder (uses real Node.js).
- **Build locked** — stop a running `Beep.OilGasSim.Api` process, then `dotnet build` again.
