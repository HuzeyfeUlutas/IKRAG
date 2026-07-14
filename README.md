# IKRAG — CV RAG İnceleme Uygulaması

İK ekiplerinin CV havuzunu bir iş ilanına göre otomatik puanlaması ve seçilen bir CV hakkında doğal dilde soru-cevap yapabilmesi için geliştirilmiş, **RAG (Retrieval-Augmented Generation) ve vector database** kavramlarını uygulamalı öğrenmek amacıyla yazılmış bir öğrenme projesi.

## Ne yapar

1. **CV Havuzu** — PDF formatında CV'ler yüklenir; metin çıkarılır, embed edilir ve parçalara (chunk) bölünerek vector veritabanına kaydedilir.
2. **İlan Eşleştirme** — Bir iş ilanı metni (ör. LinkedIn'den kopyala-yapıştır) girilir. Sistem önce vector benzerliğiyle havuzdan en uygun adayları bulur, ardından bir LLM ile bu adayları 0-100 arası puanlar ve gerekçelendirir.
3. **CV Chat** — Havuzdan seçilen bir CV hakkında doğal dilde soru sorulabilir; sistem CV'nin en alakalı parçalarını (retrieval) bulup LLM'e bağlam olarak vererek (generation) cevap üretir.

## Mimari

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
                │  + pgvector     │    │  IChatProvider        │  │  (PDF metin      │
                │  (CV, ilan,     │    │  (Ollama / OpenAI)    │  │   çıkarma)       │
                │   chunk, embed) │    │  interface + DI       │  │                  │
                └─────────────────┘    └──────────────────────┘  └──────────────────┘
```

- **Backend**: ASP.NET Core Web API (monolith), EF Core + Npgsql, `Pgvector.EntityFrameworkCore`.
- **LLM/Embedding soyutlaması**: `IEmbeddingProvider` / `IChatProvider` interface'leri sayesinde sağlayıcı değiştirilebilir. Şu an **Ollama** (yerel, ücretsiz) kullanılıyor; `appsettings.json`'daki `LlmProvider` ayarı üzerinden ileride OpenAI'a geçilebilecek şekilde tasarlandı.
- **Frontend**: React + Vite + TypeScript + Tailwind CSS + React Router.
- **Veritabanı**: PostgreSQL + [pgvector](https://github.com/pgvector/pgvector) extension — hem ilişkisel veri hem embedding vektörleri tek DB'de.

Tasarım kararlarının tam gerekçesi için: [`docs/superpowers/specs/2026-07-09-cv-rag-design.md`](docs/superpowers/specs/2026-07-09-cv-rag-design.md)

## Teknoloji Stack

| Katman | Teknoloji |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API |
| ORM | EF Core + Npgsql + Pgvector.EntityFrameworkCore |
| Veritabanı | PostgreSQL 16 + pgvector |
| PDF işleme | iText7 |
| LLM / Embedding | Ollama (`nomic-embed-text`, `llama3.1`) |
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS v4 + React Router |
| Test | xUnit |
| Ortam | Docker Compose |

## Kurulum

### Gereksinimler

- [Docker](https://www.docker.com/) & Docker Compose
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) 18+

### 1. Altyapıyı ayağa kaldır

```bash
docker compose up -d
```

Bu, PostgreSQL (pgvector extension'lı) ve Ollama container'larını başlatır.

Ollama modellerini indir (ilk kurulumda gerekli, birkaç dakika sürebilir):

```bash
docker compose exec ollama ollama pull nomic-embed-text
docker compose exec ollama ollama pull llama3.1
```

### 2. Backend'i çalıştır

```bash
cd backend/src/CvRag.Api
dotnet run
```

API varsayılan olarak `http://localhost:5147` üzerinde çalışır (bkz. `Properties/launchSettings.json`).

### 3. Frontend'i çalıştır

```bash
cd frontend
npm install
npm run dev
```

Uygulama `http://localhost:5173` üzerinde açılır; `/api` istekleri Vite dev proxy üzerinden backend'e yönlendirilir.

## Test

```bash
cd backend
dotnet test
```

Bazı testler (`MatchingServiceTests`, `CvChatServiceTests` gibi) `pgvector`'ın cosine-distance sorgularını gerçek Postgres üzerinde çalıştırdığı için canlı bir veritabanı bağlantısı gerektirir — `docker compose up -d` ile Postgres'in ayakta olduğundan emin olun.

## Proje Yapısı

```
IKRAG/
├── docker-compose.yml          # Postgres+pgvector, Ollama
├── backend/
│   ├── src/CvRag.Api/           # ASP.NET Core Web API
│   │   ├── Controllers/         # CV, iş ilanı, eşleştirme, chat endpoint'leri
│   │   ├── Data/                # EF Core DbContext
│   │   ├── Models/               # Entity'ler
│   │   ├── Services/             # İş mantığı (chunking, matching, chat, provider'lar)
│   │   └── Migrations/
│   └── tests/CvRag.Tests/        # xUnit testleri
├── frontend/
│   └── src/
│       ├── api/client.ts         # Backend API client
│       └── pages/                # CV havuzu, eşleştirme, chat sayfaları
└── docs/superpowers/
    ├── specs/                    # Tasarım dokümanı
    └── plans/                    # Implementasyon planı
```

## Notlar

- Bu proje bir **öğrenme projesi** olarak geliştirildi; kapsam bilinçli olarak sınırlı tutuldu (auth yok, tek kullanıcı, LinkedIn scraping yok — ilan metni yapıştırılıyor).
- `docker-compose.yml` içindeki Postgres kimlik bilgileri (`cvrag`/`cvrag`) yalnızca local geliştirme ortamı içindir, dışarıya açık değildir ve gerçek bir secret taşımaz.
- LLM/embedding sağlayıcısı `appsettings.json` → `LlmProvider` ayarından değiştirilebilecek şekilde soyutlanmıştır (`Ollama` | `OpenAI`); OpenAI implementasyonu şu an eklenmemiştir.
