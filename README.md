# IKRAG — CV RAG Screening App

A learning project built to get hands-on with **RAG (Retrieval-Augmented Generation) and vector database** concepts, shaped as a small HR tool: score a pool of CVs against a job posting, and chat with a single CV in natural language.

## What it does

1. **CV Pool** — Upload CVs as PDF; text is extracted, embedded, and split into chunks stored in a vector database.
2. **Job Matching** — Paste a job posting (e.g. copied from LinkedIn). The system first finds the closest candidates from the pool via vector similarity, then asks an LLM to score each one 0-100 with a short justification.
3. **CV Chat** — Ask natural-language questions about a selected CV; the system retrieves the most relevant chunks of that CV and feeds them to an LLM as context to generate an answer.

## Architecture

```
┌─────────────┐      REST API       ┌──────────────────────┐
│   React     │ ──────────────────▶ │   .NET Web API        │
│  (frontend) │ ◀────────────────── │  (ASP.NET Core)        │
└─────────────┘                     └──────────┬────────────┘
                                                │
                        ┌───────────────────────┼───────────────────────┐
                        │                        │                       │
                ┌───────▼────────┐    ┌──────────▼──────────┐  ┌────────▼────────┐
                │  PostgreSQL     │    │  IEmbeddingProvider   │  │  iText7          │
                │  + pgvector     │    │  IChatProvider        │  │  (PDF text       │
                │  (CVs, jobs,    │    │  (Ollama / OpenAI)    │  │   extraction)    │
                │   chunks, embeds)│   │  interface + DI       │  │                  │
                └─────────────────┘    └──────────────────────┘  └──────────────────┘
```

- **Backend**: ASP.NET Core Web API (monolith), EF Core + Npgsql, `Pgvector.EntityFrameworkCore`.
- **LLM/embedding abstraction**: the `IEmbeddingProvider` / `IChatProvider` interfaces make the provider swappable. Currently backed by **Ollama** (local, free); designed so a future switch to OpenAI is just a config change (`LlmProvider` in `appsettings.json`).
- **Frontend**: React + Vite + TypeScript + Tailwind CSS + React Router.
- **Database**: PostgreSQL + [pgvector](https://github.com/pgvector/pgvector) — relational data and embedding vectors live in the same database.

Full design rationale: [`docs/superpowers/specs/2026-07-09-cv-rag-design.md`](docs/superpowers/specs/2026-07-09-cv-rag-design.md)

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API |
| ORM | EF Core + Npgsql + Pgvector.EntityFrameworkCore |
| Database | PostgreSQL 16 + pgvector |
| PDF processing | iText7 |
| LLM / Embedding | Ollama (`nomic-embed-text`, `llama3.1`) |
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS v4 + React Router |
| Testing | xUnit |
| Environment | Docker Compose |

## Setup

### Requirements

- [Docker](https://www.docker.com/) & Docker Compose
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) 18+

### 1. Start the infrastructure

```bash
docker compose up -d
```

This starts PostgreSQL (with the pgvector extension) and Ollama.

Pull the required Ollama models (needed on first setup, may take a few minutes):

```bash
docker compose exec ollama ollama pull nomic-embed-text
docker compose exec ollama ollama pull llama3.1
```

### 2. Run the backend

```bash
cd backend/src/CvRag.Api
dotnet run
```

The API listens on `http://localhost:5147` by default (see `Properties/launchSettings.json`).

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

The app opens at `http://localhost:5173`; `/api` requests are proxied to the backend via the Vite dev server.

## Tests

```bash
cd backend
dotnet test
```

Some tests (`MatchingServiceTests`, `CvChatServiceTests`) run pgvector cosine-distance queries against a real Postgres instance, so a live database connection is required — make sure `docker compose up -d` has Postgres running.

## Project Structure

```
IKRAG/
├── docker-compose.yml          # Postgres+pgvector, Ollama
├── backend/
│   ├── src/CvRag.Api/           # ASP.NET Core Web API
│   │   ├── Controllers/         # CV, job posting, matching, chat endpoints
│   │   ├── Data/                # EF Core DbContext
│   │   ├── Models/               # Entities
│   │   ├── Services/             # Business logic (chunking, matching, chat, providers)
│   │   └── Migrations/
│   └── tests/CvRag.Tests/        # xUnit tests
├── frontend/
│   └── src/
│       ├── api/client.ts         # Backend API client
│       └── pages/                # CV pool, matching, chat pages
└── docs/superpowers/
    ├── specs/                    # Design document
    └── plans/                    # Implementation plan
```

## Notes

- This is a **learning project**; scope was deliberately kept narrow (no auth, single user, no LinkedIn scraping — job postings are pasted as text).
- The Postgres credentials in `docker-compose.yml` (`cvrag`/`cvrag`) are local development-only, not exposed externally, and carry no real secret.
- The LLM/embedding provider is abstracted behind `appsettings.json` → `LlmProvider` (`Ollama` | `OpenAI`); the OpenAI implementation has not been added yet.
