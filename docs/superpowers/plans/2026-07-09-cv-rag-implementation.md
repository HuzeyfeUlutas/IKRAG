# CV RAG İnceleme Uygulaması Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CV havuzu üzerinde iş ilanına göre eşleştirme/puanlama ve tek CV üzerinde RAG tabanlı soru-cevap yapan bir .NET + React uygulaması kurmak; asıl amaç RAG ve vector DB kavramlarını uygulamalı öğrenmek.

**Architecture:** ASP.NET Core Web API (monolith) + PostgreSQL/pgvector (EF Core, Npgsql) + `IEmbeddingProvider`/`IChatProvider` soyutlaması (önce Ollama, sonra opsiyonel OpenAI) + React (Vite, Tailwind) frontend. PDF metin çıkarma iText7 ile.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core + Npgsql, Pgvector.EntityFrameworkCore, PostgreSQL 16 + pgvector extension, iText7, xUnit, Ollama (nomic-embed-text + llama3.1), React 18 + Vite + TypeScript + Tailwind CSS + React Router, Docker Compose.

> **Değişiklik notu (Task 5 sırasında):** Plan başlangıçta PdfPig'i öngörmüştü, ancak nuget.org'da `UglyToad.PdfPig` paketinin yalnızca iki şüpheli/normal-dışı sürümü (`0.1.9-alpha001-patch1`, `1.7.0-custom-5`) listeli çıktı — temiz bir stabil sürüm geçmişi yoktu. Kullanıcı onayıyla PDF metin çıkarma kütüphanesi **iText7**'ye değiştirildi (nuget.org'da temiz sürüm geçmişi 7.0.1→9.7.0, AGPL açık kaynak, öğrenme projesi için uygun).

## Global Constraints

- Tek kullanıcı, auth yok (spec §1).
- LinkedIn scraping yok — ilan sadece metin yapıştırma (spec §1, §5b).
- Mikroservis yok — tek Web API projesi (spec §3).
- `CvDocument.EmbeddingVector` (tüm CV, eşleştirme) ve `CvChunk.EmbeddingVector` (parça, chat retrieval) ayrı tutulur (spec §4).
- Ollama embedding modeli `nomic-embed-text` → 768 boyutlu vektör üretir. Vector kolonları `vector(768)` olarak tanımlanır. OpenAI'a geçilirse (`text-embedding-3-small` → 1536 boyut) şema migration'ı ve mevcut verinin yeniden embed edilmesi gerekir — bu Task 21'de ele alınır, önceden bilinen bir sınırlamadır.
- Chunk boyutu ~500 karakter, 50 karakter overlap (spec §5a).
- Eşleştirmede top N = 10 aday vector similarity ile bulunur, sonra LLM ile yeniden puanlanır (spec §5b, §6).
- Chat'te top K = 4 chunk retrieval yapılır (spec §5c).
- Provider seçimi `appsettings.json` → `"LlmProvider": "Ollama" | "OpenAI"` (spec §6).

---

## Dosya Yapısı

```
IKRAG/
├── docker-compose.yml
├── backend/
│   ├── CvRag.sln
│   ├── src/CvRag.Api/
│   │   ├── CvRag.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/
│   │   │   ├── CvsController.cs
│   │   │   └── JobPostingsController.cs
│   │   ├── Data/
│   │   │   └── CvRagDbContext.cs
│   │   ├── Models/
│   │   │   ├── CvDocument.cs
│   │   │   ├── CvChunk.cs
│   │   │   ├── JobPosting.cs
│   │   │   ├── MatchResult.cs
│   │   │   └── ChatMessageEntity.cs
│   │   └── Services/
│   │       ├── IEmbeddingProvider.cs
│   │       ├── IChatProvider.cs
│   │       ├── OllamaEmbeddingProvider.cs
│   │       ├── OllamaChatProvider.cs
│   │       ├── OpenAiEmbeddingProvider.cs
│   │       ├── OpenAiChatProvider.cs
│   │       ├── PdfTextExtractor.cs
│   │       ├── TextChunker.cs
│   │       ├── MatchingService.cs
│   │       └── CvChatService.cs
│   └── tests/CvRag.Tests/
│       ├── CvRag.Tests.csproj
│       ├── TextChunkerTests.cs
│       ├── OllamaEmbeddingProviderTests.cs
│       ├── OllamaChatProviderTests.cs
│       └── MatchingServiceTests.cs
└── frontend/
    ├── package.json
    ├── tailwind.config.js
    ├── src/
    │   ├── main.tsx
    │   ├── App.tsx
    │   ├── api/client.ts
    │   └── pages/
    │       ├── CvPoolPage.tsx
    │       ├── MatchingPage.tsx
    │       └── ChatPage.tsx
```

---

## Task 1: Docker Compose Altyapısı (Postgres+pgvector, Ollama)

**Files:**
- Create: `docker-compose.yml`

**Interfaces:**
- Produces: Postgres erişilebilir `localhost:5432` (db `cvrag`, user `cvrag`, password `cvrag`), pgvector extension yüklü. Ollama erişilebilir `localhost:11434`.

- [ ] **Step 1: docker-compose.yml dosyasını oluştur**

```yaml
version: "3.9"
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: cvrag
      POSTGRES_USER: cvrag
      POSTGRES_PASSWORD: cvrag
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollamadata:/root/.ollama

volumes:
  pgdata:
  ollamadata:
```

- [ ] **Step 2: Container'ları ayağa kaldır**

Run: `docker compose up -d`
Expected: `postgres` ve `ollama` container'ları `running` durumda (`docker compose ps` ile kontrol et).

- [ ] **Step 3: pgvector extension'ının yüklü olduğunu doğrula**

Run: `docker compose exec postgres psql -U cvrag -d cvrag -c "CREATE EXTENSION IF NOT EXISTS vector; SELECT extname FROM pg_extension WHERE extname='vector';"`
Expected: çıktıda `vector` satırı görünür.

- [ ] **Step 4: Ollama modellerini indir**

Run: `docker compose exec ollama ollama pull nomic-embed-text && docker compose exec ollama ollama pull llama3.1`
Expected: her iki model için "success" mesajı.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "chore: add docker compose for postgres+pgvector and ollama"
```

---

## Task 2: .NET Web API Proje İskeleti

**Files:**
- Create: `backend/CvRag.sln`
- Create: `backend/src/CvRag.Api/CvRag.Api.csproj`
- Create: `backend/src/CvRag.Api/Program.cs`
- Create: `backend/src/CvRag.Api/appsettings.json`
- Create: `backend/tests/CvRag.Tests/CvRag.Tests.csproj`

**Interfaces:**
- Produces: `GET /api/health` endpoint döner `{"status":"ok"}`. Solution içinde `CvRag.Api` ve `CvRag.Tests` projeleri.

- [ ] **Step 1: Solution ve proje iskeletini oluştur**

Run:
```bash
mkdir -p backend/src/CvRag.Api backend/tests/CvRag.Tests
cd backend
dotnet new sln -n CvRag
dotnet new webapi -n CvRag.Api -o src/CvRag.Api --use-controllers
dotnet new xunit -n CvRag.Tests -o tests/CvRag.Tests
dotnet sln add src/CvRag.Api/CvRag.Api.csproj tests/CvRag.Tests/CvRag.Tests.csproj
cd tests/CvRag.Tests && dotnet add reference ../../src/CvRag.Api/CvRag.Api.csproj && cd ../..
```
Expected: `backend/CvRag.sln`, `backend/src/CvRag.Api/`, `backend/tests/CvRag.Tests/` oluşur; `dotnet build` hatasız tamamlanır.

- [ ] **Step 2: Health endpoint'i ekle**

`backend/src/CvRag.Api/Controllers/HealthController.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;

namespace CvRag.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
```

- [ ] **Step 3: Uygulamayı çalıştır ve health endpoint'i test et**

Run: `cd backend/src/CvRag.Api && dotnet run &`
Then: `curl http://localhost:5000/api/health` (port farklıysa `dotnet run` çıktısındaki `Now listening on:` satırına bak)
Expected: `{"status":"ok"}`
Sonra çalışan process'i durdur (`kill %1` veya terminali kapat).

- [ ] **Step 4: Commit**

```bash
git add backend/
git commit -m "chore: scaffold .NET web api and test projects"
```

---

## Task 3: EF Core + Npgsql + Pgvector Bağlantısı, DbContext İskeleti

**Files:**
- Modify: `backend/src/CvRag.Api/CvRag.Api.csproj`
- Create: `backend/src/CvRag.Api/Data/CvRagDbContext.cs`
- Modify: `backend/src/CvRag.Api/appsettings.json`
- Modify: `backend/src/CvRag.Api/Program.cs`

**Interfaces:**
- Produces: `CvRagDbContext` (boş, sonraki task'larda entity'ler eklenecek), DI'ya kayıtlı, connection string `appsettings.json`'dan okunur.

- [ ] **Step 1: Gerekli NuGet paketlerini ekle**

Run:
```bash
cd backend/src/CvRag.Api
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Pgvector
dotnet add package Pgvector.EntityFrameworkCore
cd ../../tests/CvRag.Tests
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```
Expected: `dotnet build` (backend kökünden) hatasız tamamlanır.

- [ ] **Step 2: Connection string'i appsettings.json'a ekle**

`backend/src/CvRag.Api/appsettings.json` içine `ConnectionStrings` bölümünü ekle:
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=cvrag;Username=cvrag;Password=cvrag"
  },
  "LlmProvider": "Ollama",
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "ChatModel": "llama3.1"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 3: Boş DbContext oluştur**

`backend/src/CvRag.Api/Data/CvRagDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace CvRag.Api.Data;

public class CvRagDbContext : DbContext
{
    public CvRagDbContext(DbContextOptions<CvRagDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 4: Program.cs içinde DbContext'i DI'ya kaydet**

`backend/src/CvRag.Api/Program.cs` içindeki `var builder = WebApplication.CreateBuilder(args);` satırından hemen sonra ekle:
```csharp
using CvRag.Api.Data;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<CvRagDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o => o.UseVector()));
```

- [ ] **Step 5: Build ile doğrula**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "chore: wire up ef core, npgsql and pgvector"
```

---

## Task 4: CvDocument Modeli + Migration + CV Listeleme Endpoint'i

**Files:**
- Create: `backend/src/CvRag.Api/Models/CvDocument.cs`
- Modify: `backend/src/CvRag.Api/Data/CvRagDbContext.cs`
- Create: `backend/src/CvRag.Api/Controllers/CvsController.cs`
- Create (migration, EF Core tools ile): `backend/src/CvRag.Api/Migrations/*`

**Interfaces:**
- Produces: `CvDocument` entity (`Id`, `FileName`, `RawText`, `EmbeddingVector` — bu task'ta embedding henüz doldurulmuyor, kolon nullable, `CreatedAt`). `GET /api/cvs` → `CvDocument` listesi (id, fileName, createdAt).

- [ ] **Step 1: CvDocument entity'sini oluştur**

`backend/src/CvRag.Api/Models/CvDocument.cs`:
```csharp
using Pgvector;

namespace CvRag.Api.Models;

public class CvDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public Vector? EmbeddingVector { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: DbContext'e DbSet ve vector boyutunu ekle**

`backend/src/CvRag.Api/Data/CvRagDbContext.cs` içindeki `CvRagDbContext` sınıfına ekle:
```csharp
public DbSet<CvDocument> CvDocuments => Set<CvDocument>();
```
`OnModelCreating` içine `base.OnModelCreating(modelBuilder);` satırından ÖNCE ekle:
```csharp
modelBuilder.Entity<CvDocument>()
    .Property(c => c.EmbeddingVector)
    .HasColumnType("vector(768)");
```
Dosyanın en üstüne `using CvRag.Api.Models;` ekle.

- [ ] **Step 3: Migration oluştur**

Run:
```bash
cd backend/src/CvRag.Api
dotnet tool install --global dotnet-ef --version 8.* 2>/dev/null || true
dotnet ef migrations add AddCvDocument
```
Expected: `Migrations/` klasörü altında `..._AddCvDocument.cs` dosyası oluşur, hata vermez.

- [ ] **Step 4: Migration'ı veritabanına uygula**

Run: `dotnet ef database update`
Expected: "Done." çıktısı; `docker compose exec postgres psql -U cvrag -d cvrag -c "\d \"CvDocuments\""` ile tabloyu doğrula.

- [ ] **Step 5: CV listeleme endpoint'ini yaz**

`backend/src/CvRag.Api/Controllers/CvsController.cs`:
```csharp
using CvRag.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvRag.Api.Controllers;

[ApiController]
[Route("api/cvs")]
public class CvsController : ControllerBase
{
    private readonly CvRagDbContext _db;

    public CvsController(CvRagDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var cvs = await _db.CvDocuments
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.FileName, c.CreatedAt })
            .ToListAsync();
        return Ok(cvs);
    }
}
```

- [ ] **Step 6: Manuel doğrulama**

Run: `cd backend/src/CvRag.Api && dotnet run &`
Then: `curl http://localhost:5000/api/cvs`
Expected: `[]` (henüz kayıt yok). Process'i durdur.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "feat: add CvDocument model, migration and list endpoint"
```

---

## Task 5: PDF Metin Çıkarma Servisi (iText7)

**Files:**
- Modify: `backend/src/CvRag.Api/CvRag.Api.csproj`
- Create: `backend/src/CvRag.Api/Services/PdfTextExtractor.cs`
- Create: `backend/tests/CvRag.Tests/PdfTextExtractorTests.cs`
- Create (test fixture): `backend/tests/CvRag.Tests/Fixtures/sample.pdf` (zaten mevcut — controller tarafından elle oluşturuldu, PdfPig'in nuget.org'da şüpheli sürüm geçmişi nedeniyle reportlab denemesi atlandı)

**Interfaces:**
- Produces: `PdfTextExtractor.ExtractText(Stream pdfStream) : string` — statik metod, sonraki task'larda `CvsController` tarafından kullanılacak.

- [ ] **Step 1: iText7 paketini ekle**

Run: `cd backend/src/CvRag.Api && dotnet add package itext7`
Expected: nuget.org'dan en güncel stabil sürüm (7.0.1→9.7.0 aralığında, prerelease olmayan) çözümlenir.

- [ ] **Step 2: Örnek PDF fixture'ının varlığını doğrula**

`backend/tests/CvRag.Tests/Fixtures/sample.pdf` zaten mevcut olmalı (controller tarafından elle, dış bağımlılık olmadan oluşturuldu). Doğrula:
```bash
file backend/tests/CvRag.Tests/Fixtures/sample.pdf
```
Expected: `PDF document, version 1.4, 1 pages`. Dosya yoksa DUR ve controller'a bildir — kendi başına yeniden oluşturmaya çalışma.

- [ ] **Step 3: Failing test'i yaz**

`backend/tests/CvRag.Tests/PdfTextExtractorTests.cs`:
```csharp
using CvRag.Api.Services;
using Xunit;

namespace CvRag.Tests;

public class PdfTextExtractorTests
{
    [Fact]
    public void ExtractText_ReturnsTextContainingKnownPhrase()
    {
        using var stream = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "sample.pdf"));

        var text = PdfTextExtractor.ExtractText(stream);

        Assert.Contains("Ahmet Yilmaz", text);
    }
}
```

`backend/tests/CvRag.Tests/CvRag.Tests.csproj` içine fixture'ın çıktı dizinine kopyalanması için ekle (`<ItemGroup>` içine):
```xml
<ItemGroup>
  <None Include="Fixtures/sample.pdf" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 4: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter PdfTextExtractorTests`
Expected: FAIL — "PdfTextExtractor bulunamadı" derleme hatası.

- [ ] **Step 5: PdfTextExtractor'ı implemente et**

`backend/src/CvRag.Api/Services/PdfTextExtractor.cs`:
```csharp
using System.Text;
using iText.Kernel.Pdf;

namespace CvRag.Api.Services;

public static class PdfTextExtractor
{
    public static string ExtractText(Stream pdfStream)
    {
        using var pdfDocument = new PdfDocument(new PdfReader(pdfStream));
        var sb = new StringBuilder();
        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);
            sb.AppendLine(iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page));
        }
        return sb.ToString();
    }
}
```
**Not:** iText7'nin kendi metin çıkarma sınıfı da `PdfTextExtractor` adında (`iText.Kernel.Pdf.Canvas.Parser` namespace'inde) — isim çakışmasını önlemek için bu namespace'e `using` eklenmiyor, çağrı tam nitelikli (`iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page)`) yapılıyor.

- [ ] **Step 6: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter PdfTextExtractorTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "feat: add pdf text extraction service"
```

---

## Task 6: CV Yükleme Endpoint'i (embedding olmadan)

**Files:**
- Modify: `backend/src/CvRag.Api/Controllers/CvsController.cs`
- Create: `backend/tests/CvRag.Tests/CvsControllerTests.cs`

**Interfaces:**
- Consumes: `PdfTextExtractor.ExtractText(Stream) : string` (Task 5), `CvRagDbContext.CvDocuments` (Task 4).
- Produces: `POST /api/cvs` (multipart form, `file` alanı) → `201 Created`, body `{ id, fileName, createdAt }`.

- [ ] **Step 1: Failing test'i yaz (in-memory DB ile)**

`backend/tests/CvRag.Tests/CvsControllerTests.cs`:
```csharp
using CvRag.Api.Controllers;
using CvRag.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CvRag.Tests;

public class CvsControllerTests
{
    private static CvRagDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CvRagDbContext(options);
    }

    [Fact]
    public async Task Upload_SavesCvDocumentAndReturnsCreated()
    {
        var db = CreateInMemoryDb();
        var controller = new CvsController(db);

        var bytes = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.pdf"));
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "sample.pdf");

        var result = await controller.Upload(formFile);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Single(db.CvDocuments);
        Assert.Contains("Ahmet Yilmaz", db.CvDocuments.First().RawText);
    }
}
```

- [ ] **Step 2: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter CvsControllerTests`
Expected: FAIL — `Upload` metodu bulunamadı derleme hatası.

- [ ] **Step 3: Upload endpoint'ini implemente et**

`backend/src/CvRag.Api/Controllers/CvsController.cs` içine `List` metodunun altına ekle:
```csharp
[HttpPost]
[RequestSizeLimit(10 * 1024 * 1024)]
public async Task<IActionResult> Upload(IFormFile file)
{
    if (file.Length == 0)
        return BadRequest(new { error = "Dosya boş." });

    await using var stream = file.OpenReadStream();
    var text = Services.PdfTextExtractor.ExtractText(stream);

    var cv = new Models.CvDocument
    {
        FileName = file.FileName,
        RawText = text
    };
    _db.CvDocuments.Add(cv);
    await _db.SaveChangesAsync();

    return Created($"/api/cvs/{cv.Id}", new { cv.Id, cv.FileName, cv.CreatedAt });
}
```
Dosyanın en üstüne `using Microsoft.AspNetCore.Http;` ekle (zaten yoksa).

- [ ] **Step 4: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter CvsControllerTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 5: Manuel uçtan uca doğrulama**

Run: `cd backend/src/CvRag.Api && dotnet run &`
Then: `curl -F "file=@../../tests/CvRag.Tests/Fixtures/sample.pdf" http://localhost:5000/api/cvs`
Expected: `201` status, JSON body `{"id": "...", "fileName": "sample.pdf", "createdAt": "..."}`. Process'i durdur.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat: add cv upload endpoint"
```

---

## Task 7: IEmbeddingProvider Soyutlaması + Ollama Implementasyonu

**Files:**
- Create: `backend/src/CvRag.Api/Services/IEmbeddingProvider.cs`
- Create: `backend/src/CvRag.Api/Services/OllamaEmbeddingProvider.cs`
- Create: `backend/tests/CvRag.Tests/OllamaEmbeddingProviderTests.cs`
- Modify: `backend/src/CvRag.Api/Program.cs`

**Interfaces:**
- Produces: `IEmbeddingProvider.EmbedAsync(string text) : Task<float[]>`. DI'da `IEmbeddingProvider` → `OllamaEmbeddingProvider` (appsettings `LlmProvider=Ollama` iken).

- [ ] **Step 1: Interface'i tanımla**

`backend/src/CvRag.Api/Services/IEmbeddingProvider.cs`:
```csharp
namespace CvRag.Api.Services;

public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text);
}
```

- [ ] **Step 2: Failing test'i yaz (fake HttpMessageHandler ile)**

`backend/tests/CvRag.Tests/OllamaEmbeddingProviderTests.cs`:
```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using CvRag.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CvRag.Tests;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseJson;
    public FakeHttpMessageHandler(string responseJson) => _responseJson = responseJson;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

public class OllamaEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_ParsesEmbeddingFromOllamaResponse()
    {
        var fakeJson = JsonSerializer.Serialize(new { embedding = new[] { 0.1f, 0.2f, 0.3f } });
        var handler = new FakeHttpMessageHandler(fakeJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = Options.Create(new OllamaOptions { EmbeddingModel = "nomic-embed-text" });

        var provider = new OllamaEmbeddingProvider(httpClient, options);
        var result = await provider.EmbedAsync("test metni");

        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, result);
    }
}
```

- [ ] **Step 3: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter OllamaEmbeddingProviderTests`
Expected: FAIL — `OllamaEmbeddingProvider`/`OllamaOptions` bulunamadı derleme hatası.

- [ ] **Step 4: OllamaOptions ve OllamaEmbeddingProvider'ı implemente et**

`backend/src/CvRag.Api/Services/OllamaEmbeddingProvider.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CvRag.Api.Services;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.1";
}

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingProvider(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var payload = JsonSerializer.Serialize(new { model = _options.EmbeddingModel, prompt = text });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/embeddings", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var embeddingArray = doc.RootElement.GetProperty("embedding");

        var result = new float[embeddingArray.GetArrayLength()];
        for (int i = 0; i < result.Length; i++)
            result[i] = embeddingArray[i].GetSingle();

        return result;
    }
}
```

- [ ] **Step 5: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter OllamaEmbeddingProviderTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 6: DI kaydını Program.cs'e ekle**

`backend/src/CvRag.Api/Program.cs` içine `builder.Services.AddDbContext<...>` satırının altına ekle:
```csharp
using CvRag.Api.Services;

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
});
```

- [ ] **Step 7: Build ile doğrula**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "feat: add embedding provider abstraction with ollama implementation"
```

---

## Task 8: CV Yükleme Akışına Embedding'i Bağla

**Files:**
- Modify: `backend/src/CvRag.Api/Controllers/CvsController.cs`
- Modify: `backend/tests/CvRag.Tests/CvsControllerTests.cs`

**Interfaces:**
- Consumes: `IEmbeddingProvider.EmbedAsync(string) : Task<float[]>` (Task 7).
- Produces: `CvDocument.EmbeddingVector` upload sırasında dolduruluyor.

- [ ] **Step 1: Mevcut testi güncelle — fake embedding provider ekle**

`backend/tests/CvRag.Tests/CvsControllerTests.cs` içine, `CvsControllerTests` sınıfının üstüne ekle:
```csharp
public class FakeEmbeddingProvider : CvRag.Api.Services.IEmbeddingProvider
{
    public Task<float[]> EmbedAsync(string text) => Task.FromResult(new float[768]);
}
```
`Upload_SavesCvDocumentAndReturnsCreated` testindeki `var controller = new CvsController(db);` satırını şu şekilde değiştir:
```csharp
var controller = new CvsController(db, new FakeEmbeddingProvider());
```
Test sonuna ekle:
```csharp
Assert.NotNull(db.CvDocuments.First().EmbeddingVector);
```

- [ ] **Step 2: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter CvsControllerTests`
Expected: FAIL — `CvsController` constructor'ı 2 parametre almıyor derleme hatası.

- [ ] **Step 3: CvsController'ı güncelle**

`backend/src/CvRag.Api/Controllers/CvsController.cs` içindeki constructor'ı ve alanları güncelle:
```csharp
private readonly CvRagDbContext _db;
private readonly Services.IEmbeddingProvider _embeddingProvider;

public CvsController(CvRagDbContext db, Services.IEmbeddingProvider embeddingProvider)
{
    _db = db;
    _embeddingProvider = embeddingProvider;
}
```
`Upload` metodundaki `var cv = new Models.CvDocument { ... };` satırından önce ekle:
```csharp
var embedding = await _embeddingProvider.EmbedAsync(text);
```
`CvDocument` init içine `EmbeddingVector` ekle:
```csharp
var cv = new Models.CvDocument
{
    FileName = file.FileName,
    RawText = text,
    EmbeddingVector = new Pgvector.Vector(embedding)
};
```

- [ ] **Step 4: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter CvsControllerTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 5: Ollama'nın çalıştığından emin olup manuel uçtan uca doğrula**

Run: `docker compose ps` (postgres ve ollama `running` olmalı), sonra `cd backend/src/CvRag.Api && dotnet run &`
Then: `curl -F "file=@../../tests/CvRag.Tests/Fixtures/sample.pdf" http://localhost:5000/api/cvs`
Expected: `201` status. `docker compose exec postgres psql -U cvrag -d cvrag -c "SELECT id, filename, embeddingvector IS NOT NULL AS has_embedding FROM \"CvDocuments\";"` ile `has_embedding = t` olduğunu doğrula. Process'i durdur.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat: generate and persist cv embedding on upload"
```

---

## Task 9: Metin Bölme (Chunking) Servisi

**Files:**
- Create: `backend/src/CvRag.Api/Services/TextChunker.cs`
- Create: `backend/tests/CvRag.Tests/TextChunkerTests.cs`

**Interfaces:**
- Produces: `TextChunker.Split(string text, int chunkSize = 500, int overlap = 50) : List<string>` — statik metod.

- [ ] **Step 1: Failing test'i yaz**

`backend/tests/CvRag.Tests/TextChunkerTests.cs`:
```csharp
using CvRag.Api.Services;
using Xunit;

namespace CvRag.Tests;

public class TextChunkerTests
{
    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        var result = TextChunker.Split("kısa bir metin", chunkSize: 500, overlap: 50);
        Assert.Single(result);
        Assert.Equal("kısa bir metin", result[0]);
    }

    [Fact]
    public void Split_LongText_ReturnsMultipleOverlappingChunks()
    {
        var text = new string('a', 1200);
        var result = TextChunker.Split(text, chunkSize: 500, overlap: 50);

        Assert.Equal(3, result.Count);
        Assert.Equal(500, result[0].Length);
        // İkinci chunk, birincinin son 50 karakteriyle örtüşmeli
        Assert.Equal(text.Substring(450, 50), result[1].Substring(0, 50));
    }

    [Fact]
    public void Split_EmptyText_ReturnsEmptyList()
    {
        var result = TextChunker.Split("", chunkSize: 500, overlap: 50);
        Assert.Empty(result);
    }
}
```

- [ ] **Step 2: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter TextChunkerTests`
Expected: FAIL — `TextChunker` bulunamadı derleme hatası.

- [ ] **Step 3: TextChunker'ı implemente et**

`backend/src/CvRag.Api/Services/TextChunker.cs`:
```csharp
namespace CvRag.Api.Services;

public static class TextChunker
{
    public static List<string> Split(string text, int chunkSize = 500, int overlap = 50)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text))
            return chunks;

        if (text.Length <= chunkSize)
        {
            chunks.Add(text);
            return chunks;
        }

        int start = 0;
        int step = chunkSize - overlap;
        while (start < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));
            if (start + length >= text.Length)
                break;
            start += step;
        }

        return chunks;
    }
}
```

- [ ] **Step 4: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter TextChunkerTests`
Expected: Passed! - 3 test passed.

- [ ] **Step 5: Commit**

```bash
git add backend/
git commit -m "feat: add text chunker service"
```

---

## Task 10: CvChunk Modeli + Migration + Upload Akışına Chunk Embedding'i Bağla

**Files:**
- Create: `backend/src/CvRag.Api/Models/CvChunk.cs`
- Modify: `backend/src/CvRag.Api/Data/CvRagDbContext.cs`
- Modify: `backend/src/CvRag.Api/Controllers/CvsController.cs`
- Modify: `backend/tests/CvRag.Tests/CvsControllerTests.cs`

**Interfaces:**
- Consumes: `TextChunker.Split(string, int, int) : List<string>` (Task 9), `IEmbeddingProvider.EmbedAsync` (Task 7).
- Produces: `CvChunk` entity (`Id`, `CvDocumentId`, `ChunkText`, `ChunkIndex`, `EmbeddingVector`). Upload sırasında her chunk için `CvChunk` kaydı oluşturuluyor.

- [ ] **Step 1: CvChunk entity'sini oluştur**

`backend/src/CvRag.Api/Models/CvChunk.cs`:
```csharp
using Pgvector;

namespace CvRag.Api.Models;

public class CvChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CvDocumentId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public Vector? EmbeddingVector { get; set; }
}
```

- [ ] **Step 2: DbContext'e DbSet ve vector boyutunu ekle**

`CvRagDbContext.cs` içine `CvDocuments` DbSet'inin altına ekle:
```csharp
public DbSet<CvChunk> CvChunks => Set<CvChunk>();
```
`OnModelCreating` içindeki `CvDocument` konfigürasyonunun altına ekle:
```csharp
modelBuilder.Entity<CvChunk>()
    .Property(c => c.EmbeddingVector)
    .HasColumnType("vector(768)");
```

- [ ] **Step 3: Migration oluştur ve uygula**

Run:
```bash
cd backend/src/CvRag.Api
dotnet ef migrations add AddCvChunk
dotnet ef database update
```
Expected: "Done." çıktısı.

- [ ] **Step 4: Mevcut testi güncelle — chunk oluşumunu doğrula**

`backend/tests/CvRag.Tests/CvsControllerTests.cs` içindeki test sonuna ekle:
```csharp
var chunks = db.CvChunks.Where(c => c.CvDocumentId == db.CvDocuments.First().Id).ToList();
Assert.NotEmpty(chunks);
Assert.All(chunks, c => Assert.NotNull(c.EmbeddingVector));
```

- [ ] **Step 5: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter CvsControllerTests`
Expected: FAIL — `db.CvChunks` boş (henüz chunk oluşturulmuyor).

- [ ] **Step 6: Upload akışına chunking ve chunk embedding'i ekle**

`CvsController.cs` içindeki `Upload` metodunda `await _db.SaveChangesAsync();` satırından ÖNCE, `_db.CvDocuments.Add(cv);` satırının altına ekle:
```csharp
var chunkTexts = Services.TextChunker.Split(text);
for (int i = 0; i < chunkTexts.Count; i++)
{
    var chunkEmbedding = await _embeddingProvider.EmbedAsync(chunkTexts[i]);
    _db.CvChunks.Add(new Models.CvChunk
    {
        CvDocumentId = cv.Id,
        ChunkText = chunkTexts[i],
        ChunkIndex = i,
        EmbeddingVector = new Pgvector.Vector(chunkEmbedding)
    });
}
```

- [ ] **Step 7: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter CvsControllerTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "feat: chunk cv text and persist chunk embeddings on upload"
```

---

## Task 11: JobPosting Modeli + Migration + İlan Kaydetme Endpoint'i

**Files:**
- Create: `backend/src/CvRag.Api/Models/JobPosting.cs`
- Modify: `backend/src/CvRag.Api/Data/CvRagDbContext.cs`
- Create: `backend/src/CvRag.Api/Controllers/JobPostingsController.cs`
- Create: `backend/tests/CvRag.Tests/JobPostingsControllerTests.cs`

**Interfaces:**
- Consumes: `IEmbeddingProvider.EmbedAsync` (Task 7).
- Produces: `JobPosting` entity. `POST /api/job-postings` (body `{ "text": "..." }`) → `201 Created`, body `{ id, createdAt }`.

- [ ] **Step 1: JobPosting entity'sini oluştur**

`backend/src/CvRag.Api/Models/JobPosting.cs`:
```csharp
using Pgvector;

namespace CvRag.Api.Models;

public class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RawText { get; set; } = string.Empty;
    public Vector? EmbeddingVector { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: DbContext'e DbSet ve vector boyutunu ekle**

`CvRagDbContext.cs` içine ekle:
```csharp
public DbSet<JobPosting> JobPostings => Set<JobPosting>();
```
`OnModelCreating` içine ekle:
```csharp
modelBuilder.Entity<JobPosting>()
    .Property(j => j.EmbeddingVector)
    .HasColumnType("vector(768)");
```

- [ ] **Step 3: Migration oluştur ve uygula**

Run:
```bash
cd backend/src/CvRag.Api
dotnet ef migrations add AddJobPosting
dotnet ef database update
```
Expected: "Done." çıktısı.

- [ ] **Step 4: Failing test'i yaz**

`backend/tests/CvRag.Tests/JobPostingsControllerTests.cs`:
```csharp
using CvRag.Api.Controllers;
using CvRag.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CvRag.Tests;

public class JobPostingsControllerTests
{
    private static CvRagDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CvRagDbContext(options);
    }

    [Fact]
    public async Task Create_SavesJobPostingWithEmbedding()
    {
        var db = CreateInMemoryDb();
        var controller = new JobPostingsController(db, new FakeEmbeddingProvider());

        var result = await controller.Create(new JobPostingsController.CreateRequest
        {
            Text = "Backend Developer, 3+ yil .NET deneyimi"
        });

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Single(db.JobPostings);
        Assert.NotNull(db.JobPostings.First().EmbeddingVector);
    }
}
```

- [ ] **Step 5: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter JobPostingsControllerTests`
Expected: FAIL — `JobPostingsController` bulunamadı derleme hatası.

- [ ] **Step 6: JobPostingsController'ı implemente et**

`backend/src/CvRag.Api/Controllers/JobPostingsController.cs`:
```csharp
using CvRag.Api.Data;
using CvRag.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CvRag.Api.Controllers;

[ApiController]
[Route("api/job-postings")]
public class JobPostingsController : ControllerBase
{
    private readonly CvRagDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;

    public JobPostingsController(CvRagDbContext db, IEmbeddingProvider embeddingProvider)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
    }

    public class CreateRequest
    {
        public string Text { get; set; } = string.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "İlan metni boş olamaz." });

        var embedding = await _embeddingProvider.EmbedAsync(request.Text);
        var posting = new Models.JobPosting
        {
            RawText = request.Text,
            EmbeddingVector = new Pgvector.Vector(embedding)
        };
        _db.JobPostings.Add(posting);
        await _db.SaveChangesAsync();

        return Created($"/api/job-postings/{posting.Id}", new { posting.Id, posting.CreatedAt });
    }
}
```

- [ ] **Step 7: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter JobPostingsControllerTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "feat: add job posting model and create endpoint"
```

---

## Task 12: Vector Similarity ile Top-N Aday Bulma (LLM yok, sadece pgvector)

**Files:**
- Create: `backend/src/CvRag.Api/Services/MatchingService.cs`
- Create: `backend/tests/CvRag.Tests/MatchingServiceTests.cs`

**Interfaces:**
- Consumes: `CvRagDbContext.CvDocuments` (Task 4), `Pgvector.EntityFrameworkCore` `CosineDistance` LINQ extension.
- Produces: `MatchingService.FindTopCandidatesAsync(Vector jobEmbedding, int topN) : Task<List<(CvDocument Cv, double Similarity)>>`.

- [ ] **Step 1: Failing test'i yaz (gerçek Postgres bağlantısı gerekir — bu test integration testtir)**

`backend/tests/CvRag.Tests/MatchingServiceTests.cs`:
```csharp
using CvRag.Api.Data;
using CvRag.Api.Models;
using CvRag.Api.Services;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Xunit;

namespace CvRag.Tests;

public class MatchingServiceTests : IDisposable
{
    private readonly CvRagDbContext _db;

    public MatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cvrag;Username=cvrag;Password=cvrag",
                o => o.UseVector())
            .Options;
        _db = new CvRagDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task FindTopCandidatesAsync_ReturnsClosestVectorFirst()
    {
        var closeCv = new CvDocument { FileName = "close.pdf", RawText = "x", EmbeddingVector = new Vector(MakeVector(0.9f)) };
        var farCv = new CvDocument { FileName = "far.pdf", RawText = "x", EmbeddingVector = new Vector(MakeVector(0.1f)) };
        _db.CvDocuments.AddRange(closeCv, farCv);
        await _db.SaveChangesAsync();

        var service = new MatchingService(_db);
        var jobEmbedding = new Vector(MakeVector(1.0f));

        var results = await service.FindTopCandidatesAsync(jobEmbedding, topN: 2);

        Assert.Equal("close.pdf", results[0].Cv.FileName);
    }

    private static float[] MakeVector(float value)
    {
        var arr = new float[768];
        Array.Fill(arr, value);
        return arr;
    }

    public void Dispose()
    {
        _db.CvDocuments.RemoveRange(_db.CvDocuments);
        _db.SaveChanges();
        _db.Dispose();
    }
}
```

- [ ] **Step 2: Testin fail ettiğini doğrula**

Run: `docker compose ps` (postgres çalışıyor olmalı), sonra `cd backend && dotnet test --filter MatchingServiceTests`
Expected: FAIL — `MatchingService` bulunamadı derleme hatası.

- [ ] **Step 3: MatchingService'i implemente et**

`backend/src/CvRag.Api/Services/MatchingService.cs`:
```csharp
using CvRag.Api.Data;
using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CvRag.Api.Services;

public class MatchingService
{
    private readonly CvRagDbContext _db;

    public MatchingService(CvRagDbContext db) => _db = db;

    public async Task<List<(CvDocument Cv, double Similarity)>> FindTopCandidatesAsync(Vector jobEmbedding, int topN)
    {
        var results = await _db.CvDocuments
            .Where(c => c.EmbeddingVector != null)
            .OrderBy(c => c.EmbeddingVector!.CosineDistance(jobEmbedding))
            .Take(topN)
            .Select(c => new { Cv = c, Distance = c.EmbeddingVector!.CosineDistance(jobEmbedding) })
            .ToListAsync();

        return results.Select(r => (r.Cv, 1.0 - r.Distance)).ToList();
    }
}
```

- [ ] **Step 4: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter MatchingServiceTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 5: Commit**

```bash
git add backend/
git commit -m "feat: add vector similarity matching service"
```

---

## Task 13: IChatProvider Soyutlaması + Ollama Implementasyonu

**Files:**
- Create: `backend/src/CvRag.Api/Services/IChatProvider.cs`
- Create: `backend/src/CvRag.Api/Services/OllamaChatProvider.cs`
- Create: `backend/tests/CvRag.Tests/OllamaChatProviderTests.cs`
- Modify: `backend/src/CvRag.Api/Program.cs`

**Interfaces:**
- Produces: `IChatProvider.CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage) : Task<string>`. DI'da `IChatProvider` → `OllamaChatProvider`.

- [ ] **Step 1: Interface'i tanımla**

`backend/src/CvRag.Api/Services/IChatProvider.cs`:
```csharp
namespace CvRag.Api.Services;

public interface IChatProvider
{
    Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage);
}
```

- [ ] **Step 2: Failing test'i yaz**

`backend/tests/CvRag.Tests/OllamaChatProviderTests.cs`:
```csharp
using System.Text.Json;
using CvRag.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CvRag.Tests;

public class OllamaChatProviderTests
{
    [Fact]
    public async Task CompleteAsync_ParsesAssistantContentFromOllamaResponse()
    {
        var fakeJson = JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = "Adayın 3 yıl deneyimi var." }
        });
        var handler = new FakeHttpMessageHandler(fakeJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = Options.Create(new OllamaOptions { ChatModel = "llama3.1" });

        var provider = new OllamaChatProvider(httpClient, options);
        var result = await provider.CompleteAsync(
            "Sen bir CV asistanısın.",
            new List<(string, string)>(),
            "Adayın kaç yıl deneyimi var?");

        Assert.Equal("Adayın 3 yıl deneyimi var.", result);
    }
}
```

- [ ] **Step 3: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter OllamaChatProviderTests`
Expected: FAIL — `OllamaChatProvider` bulunamadı derleme hatası.

- [ ] **Step 4: OllamaChatProvider'ı implemente et**

`backend/src/CvRag.Api/Services/OllamaChatProvider.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CvRag.Api.Services;

public class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaChatProvider(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = userMessage });

        var payload = JsonSerializer.Serialize(new
        {
            model = _options.ChatModel,
            messages,
            stream = false
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/chat", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
```

- [ ] **Step 5: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter OllamaChatProviderTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 6: DI kaydını Program.cs'e ekle**

`Program.cs` içine `IEmbeddingProvider` kaydının altına ekle:
```csharp
builder.Services.AddHttpClient<IChatProvider, OllamaChatProvider>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
});
```

- [ ] **Step 7: Build ile doğrula**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "feat: add chat provider abstraction with ollama implementation"
```

---

## Task 14: MatchResult Modeli + LLM Yeniden Sıralama + Match Endpoint'i

**Files:**
- Create: `backend/src/CvRag.Api/Models/MatchResult.cs`
- Modify: `backend/src/CvRag.Api/Data/CvRagDbContext.cs`
- Modify: `backend/src/CvRag.Api/Services/MatchingService.cs`
- Modify: `backend/src/CvRag.Api/Controllers/JobPostingsController.cs`
- Modify: `backend/tests/CvRag.Tests/MatchingServiceTests.cs`

**Interfaces:**
- Consumes: `MatchingService.FindTopCandidatesAsync` (Task 12), `IChatProvider.CompleteAsync` (Task 13).
- Produces: `MatchingService.ScoreAndRankAsync(JobPosting job, int topN) : Task<List<MatchResult>>`. `POST /api/job-postings/{id}/match` → sıralı `MatchResult` listesi.

- [ ] **Step 1: MatchResult entity'sini oluştur**

`backend/src/CvRag.Api/Models/MatchResult.cs`:
```csharp
namespace CvRag.Api.Models;

public class MatchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobPostingId { get; set; }
    public Guid CvDocumentId { get; set; }
    public double SimilarityScore { get; set; }
    public int LlmScore { get; set; }
    public string LlmReasoning { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: DbContext'e DbSet ekle ve migration oluştur**

`CvRagDbContext.cs` içine ekle:
```csharp
public DbSet<MatchResult> MatchResults => Set<MatchResult>();
```

Run:
```bash
cd backend/src/CvRag.Api
dotnet ef migrations add AddMatchResult
dotnet ef database update
```
Expected: "Done." çıktısı.

- [ ] **Step 3: Failing test'i yaz — ScoreAndRankAsync**

`backend/tests/CvRag.Tests/MatchingServiceTests.cs` içine yeni bir test ekle (aynı `IDisposable` fixture kullanılır):
```csharp
[Fact]
public async Task ScoreAndRankAsync_UsesLlmToScoreTopCandidates()
{
    var cv = new CvDocument { FileName = "aday.pdf", RawText = "5 yil .NET deneyimi", EmbeddingVector = new Vector(MakeVector(0.9f)) };
    _db.CvDocuments.Add(cv);
    var job = new JobPosting { RawText = ".NET Backend Developer araniyoo", EmbeddingVector = new Vector(MakeVector(1.0f)) };
    _db.JobPostings.Add(job);
    await _db.SaveChangesAsync();

    var fakeChat = new FakeChatProvider("{\"score\": 85, \"reasoning\": \"Gucli .NET deneyimi var.\"}");
    var service = new MatchingService(_db, fakeChat);

    var results = await service.ScoreAndRankAsync(job, topN: 1);

    Assert.Single(results);
    Assert.Equal(85, results[0].LlmScore);
    Assert.Equal("Gucli .NET deneyimi var.", results[0].LlmReasoning);
}

public class FakeChatProvider : IChatProvider
{
    private readonly string _response;
    public FakeChatProvider(string response) => _response = response;
    public Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage)
        => Task.FromResult(_response);
}
```
Dosyanın en üstüne `using CvRag.Api.Services;` zaten var, `System.Text.Json` ekle (JSON parse için implementasyonda kullanılacak, testte gerek yok).

- [ ] **Step 4: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter MatchingServiceTests`
Expected: FAIL — `MatchingService` constructor'ı 1 parametre alıyor, `ScoreAndRankAsync` bulunamıyor.

- [ ] **Step 5: MatchingService'i güncelle**

`backend/src/CvRag.Api/Services/MatchingService.cs` dosyasının tamamını şu şekilde güncelle:
```csharp
using System.Text.Json;
using CvRag.Api.Data;
using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CvRag.Api.Services;

public class MatchingService
{
    private readonly CvRagDbContext _db;
    private readonly IChatProvider _chatProvider;

    public MatchingService(CvRagDbContext db, IChatProvider chatProvider)
    {
        _db = db;
        _chatProvider = chatProvider;
    }

    public async Task<List<(CvDocument Cv, double Similarity)>> FindTopCandidatesAsync(Vector jobEmbedding, int topN)
    {
        var results = await _db.CvDocuments
            .Where(c => c.EmbeddingVector != null)
            .OrderBy(c => c.EmbeddingVector!.CosineDistance(jobEmbedding))
            .Take(topN)
            .Select(c => new { Cv = c, Distance = c.EmbeddingVector!.CosineDistance(jobEmbedding) })
            .ToListAsync();

        return results.Select(r => (r.Cv, 1.0 - r.Distance)).ToList();
    }

    public async Task<List<MatchResult>> ScoreAndRankAsync(JobPosting job, int topN)
    {
        var candidates = await FindTopCandidatesAsync(job.EmbeddingVector!, topN);
        var results = new List<MatchResult>();

        foreach (var (cv, similarity) in candidates)
        {
            var systemPrompt =
                "Sen bir İK asistanısın. Sana bir iş ilanı ve bir CV metni verilecek. " +
                "CV'nin ilana uygunluğunu 0-100 arasında puanla ve kısa bir gerekçe yaz. " +
                "Yanıtını SADECE şu JSON formatında ver: {\"score\": <int>, \"reasoning\": \"<kısa metin>\"}";
            var userMessage = $"İlan:\n{job.RawText}\n\nCV:\n{cv.RawText}";

            var response = await _chatProvider.CompleteAsync(systemPrompt, new List<(string, string)>(), userMessage);
            using var doc = JsonDocument.Parse(response);
            var score = doc.RootElement.GetProperty("score").GetInt32();
            var reasoning = doc.RootElement.GetProperty("reasoning").GetString() ?? string.Empty;

            var matchResult = new MatchResult
            {
                JobPostingId = job.Id,
                CvDocumentId = cv.Id,
                SimilarityScore = similarity,
                LlmScore = score,
                LlmReasoning = reasoning
            };
            _db.MatchResults.Add(matchResult);
            results.Add(matchResult);
        }

        await _db.SaveChangesAsync();
        return results.OrderByDescending(r => r.LlmScore).ToList();
    }
}
```

- [ ] **Step 6: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter MatchingServiceTests`
Expected: Passed! - 2 test passed.

- [ ] **Step 7: Match endpoint'ini JobPostingsController'a ekle**

`backend/src/CvRag.Api/Controllers/JobPostingsController.cs` içindeki constructor'ı güncelle ve yeni endpoint ekle:
```csharp
private readonly CvRagDbContext _db;
private readonly IEmbeddingProvider _embeddingProvider;
private readonly MatchingService _matchingService;

public JobPostingsController(CvRagDbContext db, IEmbeddingProvider embeddingProvider, MatchingService matchingService)
{
    _db = db;
    _embeddingProvider = embeddingProvider;
    _matchingService = matchingService;
}
```
`Create` metodunun altına ekle:
```csharp
[HttpPost("{id}/match")]
public async Task<IActionResult> Match(Guid id, [FromQuery] int topN = 10)
{
    var job = await _db.JobPostings.FindAsync(id);
    if (job is null)
        return NotFound(new { error = "İlan bulunamadı." });

    var results = await _matchingService.ScoreAndRankAsync(job, topN);

    var response = results.Select(r => new
    {
        r.CvDocumentId,
        r.SimilarityScore,
        r.LlmScore,
        r.LlmReasoning
    });

    return Ok(response);
}
```

- [ ] **Step 8: MatchingService'i DI'ya kaydet**

`backend/src/CvRag.Api/Program.cs` içine `IChatProvider` kaydının altına ekle:
```csharp
builder.Services.AddScoped<MatchingService>();
```

- [ ] **Step 9: Build ile doğrula**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 10: Commit**

```bash
git add backend/
git commit -m "feat: add llm-based match scoring and match endpoint"
```

---

## Task 15: ChatMessage Modeli + CV Chat Servisi (Retrieval + Generation)

**Files:**
- Create: `backend/src/CvRag.Api/Models/ChatMessageEntity.cs`
- Modify: `backend/src/CvRag.Api/Data/CvRagDbContext.cs`
- Create: `backend/src/CvRag.Api/Services/CvChatService.cs`
- Create: `backend/tests/CvRag.Tests/CvChatServiceTests.cs`
- Modify: `backend/src/CvRag.Api/Controllers/CvsController.cs`

**Interfaces:**
- Consumes: `IEmbeddingProvider.EmbedAsync` (Task 7), `IChatProvider.CompleteAsync` (Task 13), `CvRagDbContext.CvChunks` (Task 10).
- Produces: `CvChatService.AskAsync(Guid cvId, string question) : Task<string>`. `POST /api/cvs/{id}/chat` → `{ answer }`.

- [ ] **Step 1: ChatMessageEntity'i oluştur**

`backend/src/CvRag.Api/Models/ChatMessageEntity.cs`:
```csharp
namespace CvRag.Api.Models;

public class ChatMessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CvDocumentId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: DbContext'e DbSet ekle ve migration oluştur**

`CvRagDbContext.cs` içine ekle:
```csharp
public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
```

Run:
```bash
cd backend/src/CvRag.Api
dotnet ef migrations add AddChatMessage
dotnet ef database update
```
Expected: "Done." çıktısı.

- [ ] **Step 3: Failing test'i yaz**

`backend/tests/CvRag.Tests/CvChatServiceTests.cs`:
```csharp
using CvRag.Api.Data;
using CvRag.Api.Models;
using CvRag.Api.Services;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Xunit;

namespace CvRag.Tests;

public class CvChatServiceTests : IDisposable
{
    private readonly CvRagDbContext _db;

    public CvChatServiceTests()
    {
        var options = new DbContextOptionsBuilder<CvRagDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cvrag;Username=cvrag;Password=cvrag",
                o => o.UseVector())
            .Options;
        _db = new CvRagDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task AskAsync_RetrievesRelevantChunksAndReturnsAnswer()
    {
        var cv = new CvDocument { FileName = "aday.pdf", RawText = "..." };
        _db.CvDocuments.Add(cv);
        _db.CvChunks.Add(new CvChunk
        {
            CvDocumentId = cv.Id,
            ChunkText = "5 yıl .NET backend deneyimi",
            ChunkIndex = 0,
            EmbeddingVector = new Vector(MakeVector(0.9f))
        });
        await _db.SaveChangesAsync();

        var fakeEmbedding = new FakeEmbeddingProviderReturning(MakeVector(1.0f));
        var fakeChat = new RecordingChatProvider("Adayın 5 yıl .NET deneyimi var.");
        var service = new CvChatService(_db, fakeEmbedding, fakeChat);

        var answer = await service.AskAsync(cv.Id, "Kaç yıl deneyimi var?");

        Assert.Equal("Adayın 5 yıl .NET deneyimi var.", answer);
        Assert.Contains("5 yıl .NET backend deneyimi", fakeChat.LastUserMessage);
        Assert.Equal(2, _db.ChatMessages.Count(m => m.CvDocumentId == cv.Id));
    }

    private static float[] MakeVector(float value)
    {
        var arr = new float[768];
        Array.Fill(arr, value);
        return arr;
    }

    public void Dispose()
    {
        _db.ChatMessages.RemoveRange(_db.ChatMessages);
        _db.CvChunks.RemoveRange(_db.CvChunks);
        _db.CvDocuments.RemoveRange(_db.CvDocuments);
        _db.SaveChanges();
        _db.Dispose();
    }
}

public class FakeEmbeddingProviderReturning : IEmbeddingProvider
{
    private readonly float[] _vector;
    public FakeEmbeddingProviderReturning(float[] vector) => _vector = vector;
    public Task<float[]> EmbedAsync(string text) => Task.FromResult(_vector);
}

public class RecordingChatProvider : IChatProvider
{
    private readonly string _response;
    public string LastUserMessage { get; private set; } = string.Empty;
    public RecordingChatProvider(string response) => _response = response;
    public Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        LastUserMessage = userMessage;
        return Task.FromResult(_response);
    }
}
```

- [ ] **Step 4: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter CvChatServiceTests`
Expected: FAIL — `CvChatService` bulunamadı derleme hatası.

- [ ] **Step 5: CvChatService'i implemente et**

`backend/src/CvRag.Api/Services/CvChatService.cs`:
```csharp
using CvRag.Api.Data;
using CvRag.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace CvRag.Api.Services;

public class CvChatService
{
    private const int TopK = 4;
    private readonly CvRagDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IChatProvider _chatProvider;

    public CvChatService(CvRagDbContext db, IEmbeddingProvider embeddingProvider, IChatProvider chatProvider)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _chatProvider = chatProvider;
    }

    public async Task<string> AskAsync(Guid cvId, string question)
    {
        var questionEmbedding = await _embeddingProvider.EmbedAsync(question);
        var queryVector = new Pgvector.Vector(questionEmbedding);

        var relevantChunks = await _db.CvChunks
            .Where(c => c.CvDocumentId == cvId && c.EmbeddingVector != null)
            .OrderBy(c => c.EmbeddingVector!.CosineDistance(queryVector))
            .Take(TopK)
            .Select(c => c.ChunkText)
            .ToListAsync();

        var history = await _db.ChatMessages
            .Where(m => m.CvDocumentId == cvId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ValueTuple<string, string>(m.Role, m.Content))
            .ToListAsync();

        var systemPrompt =
            "Sen bir İK asistanısın. Sana bir adayın CV'sinden alınmış ilgili parçalar verilecek. " +
            "Sadece bu parçalara dayanarak soruyu cevapla. Parçalarda cevap yoksa bunu belirt.";
        var context = string.Join("\n---\n", relevantChunks);
        var userMessage = $"CV parçaları:\n{context}\n\nSoru: {question}";

        var answer = await _chatProvider.CompleteAsync(systemPrompt, history, userMessage);

        _db.ChatMessages.Add(new ChatMessageEntity { CvDocumentId = cvId, Role = "user", Content = question });
        _db.ChatMessages.Add(new ChatMessageEntity { CvDocumentId = cvId, Role = "assistant", Content = answer });
        await _db.SaveChangesAsync();

        return answer;
    }
}
```

- [ ] **Step 6: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter CvChatServiceTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 7: Chat endpoint'ini CvsController'a ekle**

`CvsController.cs` içindeki constructor'ı güncelle ve yeni endpoint ekle:
```csharp
private readonly CvRagDbContext _db;
private readonly Services.IEmbeddingProvider _embeddingProvider;
private readonly Services.CvChatService _chatService;

public CvsController(CvRagDbContext db, Services.IEmbeddingProvider embeddingProvider, Services.CvChatService chatService)
{
    _db = db;
    _embeddingProvider = embeddingProvider;
    _chatService = chatService;
}

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
}

[HttpPost("{id}/chat")]
public async Task<IActionResult> Chat(Guid id, [FromBody] ChatRequest request)
{
    var exists = await _db.CvDocuments.AnyAsync(c => c.Id == id);
    if (!exists)
        return NotFound(new { error = "CV bulunamadı." });

    var answer = await _chatService.AskAsync(id, request.Question);
    return Ok(new { answer });
}
```
Dosyanın en üstüne `using Microsoft.EntityFrameworkCore;` ekle (yoksa).

- [ ] **Step 8: CvChatService'i DI'ya kaydet**

`Program.cs` içine `MatchingService` kaydının altına ekle:
```csharp
builder.Services.AddScoped<CvChatService>();
```

- [ ] **Step 9: Build ile doğrula**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 10: Commit**

```bash
git add backend/
git commit -m "feat: add cv chat service with chunk retrieval and chat endpoint"
```

---

## Task 16: React + Vite + Tailwind Proje İskeleti

**Files:**
- Create: `frontend/` (Vite scaffold)
- Modify: `frontend/tailwind.config.js`
- Modify: `frontend/src/index.css`
- Create: `frontend/src/App.tsx`
- Create: `frontend/src/main.tsx`

**Interfaces:**
- Produces: Vite dev server `localhost:5173`, React Router ile 3 boş route (`/`, `/match`, `/chat`), Tailwind çalışır durumda.

- [ ] **Step 1: Vite React-TS projesini oluştur**

Run:
```bash
cd /Users/huzeyfeulutas/Desktop/Garage/IKRAG
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
npm install react-router-dom
```
Expected: `frontend/` altında Vite proje dosyaları oluşur, `npm install` hatasız tamamlanır.

- [ ] **Step 2: Tailwind'i yapılandır**

`frontend/tailwind.config.js`:
```js
/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: { extend: {} },
  plugins: [],
}
```

`frontend/src/index.css` dosyasının en üstüne ekle:
```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

- [ ] **Step 3: React Router ile 3 boş sayfa ve navigasyonu kur**

`frontend/src/App.tsx`:
```tsx
import { BrowserRouter, Routes, Route, NavLink } from "react-router-dom";
import CvPoolPage from "./pages/CvPoolPage";
import MatchingPage from "./pages/MatchingPage";
import ChatPage from "./pages/ChatPage";

function App() {
  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `px-4 py-2 rounded ${isActive ? "bg-blue-600 text-white" : "text-blue-600"}`;

  return (
    <BrowserRouter>
      <nav className="flex gap-2 p-4 border-b">
        <NavLink to="/" end className={linkClass}>CV Havuzu</NavLink>
        <NavLink to="/match" className={linkClass}>Eşleştirme</NavLink>
        <NavLink to="/chat" className={linkClass}>CV Chat</NavLink>
      </nav>
      <main className="p-6">
        <Routes>
          <Route path="/" element={<CvPoolPage />} />
          <Route path="/match" element={<MatchingPage />} />
          <Route path="/chat" element={<ChatPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}

export default App;
```

Geçici olarak (Task 17-19'da doldurulacak) boş sayfa bileşenleri oluştur:

`frontend/src/pages/CvPoolPage.tsx`:
```tsx
export default function CvPoolPage() {
  return <div>CV Havuzu (yakında)</div>;
}
```

`frontend/src/pages/MatchingPage.tsx`:
```tsx
export default function MatchingPage() {
  return <div>Eşleştirme (yakında)</div>;
}
```

`frontend/src/pages/ChatPage.tsx`:
```tsx
export default function ChatPage() {
  return <div>CV Chat (yakında)</div>;
}
```

- [ ] **Step 4: Dev server'ı çalıştır ve manuel doğrula**

Run: `cd frontend && npm run dev &`
Then: Tarayıcıda `http://localhost:5173` aç.
Expected: Üstte 3 navigasyon linki görünür, tıklayınca sayfa içerikleri değişir, Tailwind stilleri (mavi renkler, padding) uygulanmış görünür. Dev server'ı durdur (`kill %1`).

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "chore: scaffold react + vite + tailwind frontend"
```

---

## Task 17: CV Havuzu Sayfası (Upload + Liste)

**Files:**
- Create: `frontend/src/api/client.ts`
- Modify: `frontend/src/pages/CvPoolPage.tsx`
- Modify: `frontend/vite.config.ts`

**Interfaces:**
- Consumes: `GET /api/cvs` (Task 4), `POST /api/cvs` (Task 6/8).
- Produces: `apiClient.listCvs()`, `apiClient.uploadCv(file)` — sonraki sayfalarda da kullanılacak paylaşılan API client.

- [ ] **Step 1: Vite dev server proxy'sini backend'e yönlendir**

`frontend/vite.config.ts` içindeki `defineConfig` çağrısına `server` bölümü ekle:
```ts
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": "http://localhost:5000",
    },
  },
})
```

- [ ] **Step 2: API client'ı oluştur**

`frontend/src/api/client.ts`:
```ts
export interface CvSummary {
  id: string;
  fileName: string;
  createdAt: string;
}

export async function listCvs(): Promise<CvSummary[]> {
  const res = await fetch("/api/cvs");
  if (!res.ok) throw new Error("CV listesi alınamadı");
  return res.json();
}

export async function uploadCv(file: File): Promise<CvSummary> {
  const formData = new FormData();
  formData.append("file", file);
  const res = await fetch("/api/cvs", { method: "POST", body: formData });
  if (!res.ok) throw new Error("CV yüklenemedi");
  return res.json();
}
```

- [ ] **Step 3: CvPoolPage'i implemente et**

`frontend/src/pages/CvPoolPage.tsx`:
```tsx
import { useEffect, useState } from "react";
import { listCvs, uploadCv, type CvSummary } from "../api/client";

export default function CvPoolPage() {
  const [cvs, setCvs] = useState<CvSummary[]>([]);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = () => listCvs().then(setCvs).catch((e) => setError(e.message));

  useEffect(() => { refresh(); }, []);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    setError(null);
    try {
      await uploadCv(file);
      await refresh();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setUploading(false);
      e.target.value = "";
    }
  };

  return (
    <div className="max-w-xl">
      <h1 className="text-xl font-semibold mb-4">CV Havuzu</h1>
      <input type="file" accept="application/pdf" onChange={handleFileChange} disabled={uploading} />
      {uploading && <p className="text-sm text-gray-500 mt-2">Yükleniyor...</p>}
      {error && <p className="text-sm text-red-600 mt-2">{error}</p>}
      <ul className="mt-6 space-y-2">
        {cvs.map((cv) => (
          <li key={cv.id} className="border rounded p-3">
            <div className="font-medium">{cv.fileName}</div>
            <div className="text-sm text-gray-500">{new Date(cv.createdAt).toLocaleString()}</div>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

- [ ] **Step 4: Manuel uçtan uca doğrulama**

Run: backend'i çalıştır (`cd backend/src/CvRag.Api && dotnet run &`), sonra frontend'i çalıştır (`cd frontend && npm run dev &`)
Then: `http://localhost:5173` adresinde bir PDF yükle.
Expected: Yükleme sonrası liste anında güncellenir, dosya adı ve tarih görünür. İki process'i de durdur.

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "feat: implement cv pool page with upload and listing"
```

---

## Task 18: Eşleştirme Sayfası

**Files:**
- Modify: `frontend/src/api/client.ts`
- Modify: `frontend/src/pages/MatchingPage.tsx`

**Interfaces:**
- Consumes: `POST /api/job-postings` (Task 11), `POST /api/job-postings/{id}/match` (Task 14).

- [ ] **Step 1: API client'a eşleştirme fonksiyonlarını ekle**

`frontend/src/api/client.ts` sonuna ekle:
```ts
export interface MatchResultItem {
  cvDocumentId: string;
  similarityScore: number;
  llmScore: number;
  llmReasoning: string;
}

export async function createJobPosting(text: string): Promise<{ id: string }> {
  const res = await fetch("/api/job-postings", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text }),
  });
  if (!res.ok) throw new Error("İlan kaydedilemedi");
  return res.json();
}

export async function matchJobPosting(jobId: string): Promise<MatchResultItem[]> {
  const res = await fetch(`/api/job-postings/${jobId}/match`, { method: "POST" });
  if (!res.ok) throw new Error("Eşleştirme başarısız");
  return res.json();
}
```

- [ ] **Step 2: MatchingPage'i implemente et**

`frontend/src/pages/MatchingPage.tsx`:
```tsx
import { useState } from "react";
import { createJobPosting, matchJobPosting, type MatchResultItem } from "../api/client";

export default function MatchingPage() {
  const [jobText, setJobText] = useState("");
  const [results, setResults] = useState<MatchResultItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleMatch = async () => {
    if (!jobText.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const job = await createJobPosting(jobText);
      const matches = await matchJobPosting(job.id);
      setResults(matches.sort((a, b) => b.llmScore - a.llmScore));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-2xl">
      <h1 className="text-xl font-semibold mb-4">İlan & Eşleştirme</h1>
      <textarea
        className="w-full border rounded p-2 h-40"
        placeholder="LinkedIn ilan metnini buraya yapıştır..."
        value={jobText}
        onChange={(e) => setJobText(e.target.value)}
      />
      <button
        className="mt-3 bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-50"
        onClick={handleMatch}
        disabled={loading || !jobText.trim()}
      >
        {loading ? "Eşleştiriliyor..." : "Eşleştir"}
      </button>
      {error && <p className="text-sm text-red-600 mt-2">{error}</p>}
      <ul className="mt-6 space-y-3">
        {results.map((r) => (
          <li key={r.cvDocumentId} className="border rounded p-3">
            <div className="flex justify-between">
              <span className="font-medium">CV: {r.cvDocumentId}</span>
              <span className="font-semibold">{r.llmScore}/100</span>
            </div>
            <p className="text-sm text-gray-600 mt-1">{r.llmReasoning}</p>
            <p className="text-xs text-gray-400 mt-1">Benzerlik: {(r.similarityScore * 100).toFixed(1)}%</p>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

- [ ] **Step 3: Manuel uçtan uca doğrulama**

Run: backend ve frontend'i çalıştır, en az bir CV yüklenmiş olsun.
Then: Eşleştirme sayfasında bir ilan metni yapıştır, "Eşleştir" tıkla.
Expected: Birkaç saniye sonra puanlanmış CV listesi görünür, en yüksek puanlı en üstte.

- [ ] **Step 4: Commit**

```bash
git add frontend/
git commit -m "feat: implement job matching page"
```

---

## Task 19: CV Chat Sayfası

**Files:**
- Modify: `frontend/src/api/client.ts`
- Modify: `frontend/src/pages/ChatPage.tsx`

**Interfaces:**
- Consumes: `GET /api/cvs` (Task 4), `POST /api/cvs/{id}/chat` (Task 15).

- [ ] **Step 1: API client'a chat fonksiyonunu ekle**

`frontend/src/api/client.ts` sonuna ekle:
```ts
export async function chatWithCv(cvId: string, question: string): Promise<string> {
  const res = await fetch(`/api/cvs/${cvId}/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question }),
  });
  if (!res.ok) throw new Error("Chat isteği başarısız");
  const data = await res.json();
  return data.answer;
}
```

- [ ] **Step 2: ChatPage'i implemente et**

`frontend/src/pages/ChatPage.tsx`:
```tsx
import { useEffect, useState } from "react";
import { listCvs, chatWithCv, type CvSummary } from "../api/client";

interface Message {
  role: "user" | "assistant";
  content: string;
}

export default function ChatPage() {
  const [cvs, setCvs] = useState<CvSummary[]>([]);
  const [selectedCvId, setSelectedCvId] = useState("");
  const [messages, setMessages] = useState<Message[]>([]);
  const [question, setQuestion] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => { listCvs().then(setCvs); }, []);

  const handleSend = async () => {
    if (!question.trim() || !selectedCvId) return;
    const userMessage: Message = { role: "user", content: question };
    setMessages((prev) => [...prev, userMessage]);
    setQuestion("");
    setLoading(true);
    try {
      const answer = await chatWithCv(selectedCvId, userMessage.content);
      setMessages((prev) => [...prev, { role: "assistant", content: answer }]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-2xl">
      <h1 className="text-xl font-semibold mb-4">CV Chat</h1>
      <select
        className="border rounded p-2 w-full mb-4"
        value={selectedCvId}
        onChange={(e) => { setSelectedCvId(e.target.value); setMessages([]); }}
      >
        <option value="">CV seç...</option>
        {cvs.map((cv) => (
          <option key={cv.id} value={cv.id}>{cv.fileName}</option>
        ))}
      </select>

      <div className="border rounded p-3 h-80 overflow-y-auto space-y-2 mb-3">
        {messages.map((m, i) => (
          <div key={i} className={m.role === "user" ? "text-right" : "text-left"}>
            <span className={`inline-block px-3 py-2 rounded ${m.role === "user" ? "bg-blue-600 text-white" : "bg-gray-100"}`}>
              {m.content}
            </span>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <input
          className="flex-1 border rounded p-2"
          placeholder="Bir soru sor..."
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && handleSend()}
          disabled={!selectedCvId || loading}
        />
        <button
          className="bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-50"
          onClick={handleSend}
          disabled={!selectedCvId || loading || !question.trim()}
        >
          Gönder
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Manuel uçtan uca doğrulama**

Run: backend ve frontend'i çalıştır, en az bir CV yüklenmiş olsun.
Then: Chat sayfasında bir CV seç, "Kaç yıl deneyimi var?" gibi bir soru sor.
Expected: Kullanıcı mesajı sağda, asistan cevabı solda balon olarak görünür; cevap CV içeriğine dayalı olmalı.

- [ ] **Step 4: Commit**

```bash
git add frontend/
git commit -m "feat: implement cv chat page"
```

---

## Task 20: Global Hata Yönetimi Middleware

**Files:**
- Create: `backend/src/CvRag.Api/Middleware/ExceptionHandlingMiddleware.cs`
- Modify: `backend/src/CvRag.Api/Program.cs`

**Interfaces:**
- Produces: Yakalanmamış her exception `500` + `{ "error": "..." }` JSON formatında döner; `HttpRequestException` (Ollama'ya ulaşılamama gibi) özel olarak anlamlı mesajla `503` döner.

- [ ] **Step 1: Middleware'i yaz**

`backend/src/CvRag.Api/Middleware/ExceptionHandlingMiddleware.cs`:
```csharp
using System.Text.Json;

namespace CvRag.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM servisine ulaşılamadı");
            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Ollama servisine ulaşılamadı. Docker container'ının çalıştığından emin olun."
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beklenmeyen hata");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Beklenmeyen bir hata oluştu."
            }));
        }
    }
}
```

- [ ] **Step 2: Middleware'i pipeline'a ekle**

`Program.cs` içinde `var app = builder.Build();` satırından hemen sonra, diğer middleware'lerden önce ekle:
```csharp
app.UseMiddleware<CvRag.Api.Middleware.ExceptionHandlingMiddleware>();
```

- [ ] **Step 3: Manuel doğrulama — Ollama'yı durdurup hata mesajını gözlemle**

Run: `docker compose stop ollama`, sonra backend'i çalıştır ve bir CV yükle (`curl -F "file=@..." http://localhost:5000/api/cvs`)
Expected: `503` status, body `{"error": "Ollama servisine ulaşılamadı. ..."}`. Sonra `docker compose start ollama` ile geri başlat.

- [ ] **Step 4: Commit**

```bash
git add backend/
git commit -m "feat: add global exception handling middleware"
```

---

## Task 21 (Opsiyonel, son adım): OpenAI Provider Implementasyonu

**Files:**
- Create: `backend/src/CvRag.Api/Services/OpenAiEmbeddingProvider.cs`
- Create: `backend/src/CvRag.Api/Services/OpenAiChatProvider.cs`
- Create: `backend/tests/CvRag.Tests/OpenAiEmbeddingProviderTests.cs`
- Modify: `backend/src/CvRag.Api/Program.cs`
- Modify: `backend/src/CvRag.Api/appsettings.json`

**Interfaces:**
- Consumes: `IEmbeddingProvider`, `IChatProvider` (Task 7, 13).
- Produces: `appsettings.json`'da `"LlmProvider": "OpenAI"` iken bu implementasyonlar DI'da devreye girer.

**Not:** OpenAI `text-embedding-3-small` 1536 boyutlu vektör üretir, mevcut şema `vector(768)`. Bu nedenle bu task'ı uygularken önce yeni bir migration ile vector kolonlarının boyutunu değiştirmen (`vector(1536)`) ve mevcut CV/chunk/ilan verilerini yeniden embed etmen gerekir — bu adım Step 6'da ele alınıyor.

- [ ] **Step 1: OpenAI NuGet paketini ekle**

Run: `cd backend/src/CvRag.Api && dotnet add package OpenAI`

- [ ] **Step 2: appsettings.json'a OpenAI ayarlarını ekle**

`appsettings.json` içine `Ollama` bölümünün yanına ekle:
```json
"OpenAI": {
  "ApiKey": "",
  "EmbeddingModel": "text-embedding-3-small",
  "ChatModel": "gpt-4o-mini"
}
```
API anahtarını `dotnet user-secrets` ile ayrı tutman önerilir:
```bash
cd backend/src/CvRag.Api
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
```

- [ ] **Step 3: Failing test'i yaz**

`backend/tests/CvRag.Tests/OpenAiEmbeddingProviderTests.cs`:
```csharp
using CvRag.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CvRag.Tests;

public class OpenAiEmbeddingProviderTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenApiKeyProvided()
    {
        var options = Options.Create(new OpenAiOptions { ApiKey = "sk-test", EmbeddingModel = "text-embedding-3-small" });
        var provider = new OpenAiEmbeddingProvider(options);
        Assert.NotNull(provider);
    }
}
```

- [ ] **Step 4: Testin fail ettiğini doğrula**

Run: `cd backend && dotnet test --filter OpenAiEmbeddingProviderTests`
Expected: FAIL — `OpenAiOptions`/`OpenAiEmbeddingProvider` bulunamadı derleme hatası.

- [ ] **Step 5: OpenAI implementasyonlarını yaz**

`backend/src/CvRag.Api/Services/OpenAiEmbeddingProvider.cs`:
```csharp
using OpenAI;
using OpenAI.Embeddings;
using Microsoft.Extensions.Options;

namespace CvRag.Api.Services;

public class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4o-mini";
}

public class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingClient _client;

    public OpenAiEmbeddingProvider(IOptions<OpenAiOptions> options)
    {
        _client = new EmbeddingClient(options.Value.EmbeddingModel, options.Value.ApiKey);
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var result = await _client.GenerateEmbeddingAsync(text);
        return result.Value.ToFloats().ToArray();
    }
}
```

`backend/src/CvRag.Api/Services/OpenAiChatProvider.cs`:
```csharp
using OpenAI.Chat;
using Microsoft.Extensions.Options;

namespace CvRag.Api.Services;

public class OpenAiChatProvider : IChatProvider
{
    private readonly ChatClient _client;

    public OpenAiChatProvider(IOptions<OpenAiOptions> options)
    {
        _client = new ChatClient(options.Value.ChatModel, options.Value.ApiKey);
    }

    public async Task<string> CompleteAsync(string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };
        foreach (var (role, content) in history)
        {
            messages.Add(role == "user" ? new UserChatMessage(content) : new AssistantChatMessage(content));
        }
        messages.Add(new UserChatMessage(userMessage));

        var result = await _client.CompleteChatAsync(messages);
        return result.Value.Content[0].Text;
    }
}
```

- [ ] **Step 6: Testin geçtiğini doğrula**

Run: `cd backend && dotnet test --filter OpenAiEmbeddingProviderTests`
Expected: Passed! - 1 test passed.

- [ ] **Step 7: DI'da provider seçimini appsettings'e göre yap**

`Program.cs` içindeki mevcut `AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>` ve `AddHttpClient<IChatProvider, OllamaChatProvider>` kayıtlarını kaldırıp yerine ekle:
```csharp
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));

var llmProvider = builder.Configuration["LlmProvider"] ?? "Ollama";
if (llmProvider == "OpenAI")
{
    builder.Services.AddSingleton<IEmbeddingProvider, OpenAiEmbeddingProvider>();
    builder.Services.AddSingleton<IChatProvider, OpenAiChatProvider>();
}
else
{
    builder.Services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>(client =>
    {
        var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        client.BaseAddress = new Uri(baseUrl);
    });
    builder.Services.AddHttpClient<IChatProvider, OllamaChatProvider>(client =>
    {
        var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        client.BaseAddress = new Uri(baseUrl);
    });
}
```

- [ ] **Step 8: Vector boyutu migration'ı (OpenAI'a geçerken)**

Bu adım sadece `LlmProvider` gerçekten `OpenAI`'a çevrildiğinde uygulanır:
```bash
cd backend/src/CvRag.Api
dotnet ef migrations add ChangeVectorDimensionTo1536
```
Oluşan migration dosyasında `vector(768)` geçen tüm `AlterColumn` çağrılarını `vector(1536)` olarak düzenle, sonra:
```bash
dotnet ef database update
```
**Uyarı:** Bu migration mevcut embedding verisini geçersiz kılar (boyut değiştiği için) — tüm CV, chunk ve ilan kayıtlarının yeniden yüklenip embed edilmesi gerekir (mevcut kayıtları silip yeniden upload etmek en basit yol).

- [ ] **Step 9: Build ile doğrula**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 10: Commit**

```bash
git add backend/
git commit -m "feat: add openai provider implementation with config-based switch"
```

---

## Self-Review Notları

- **Spec kapsama:** §1 (3 ana yetenek) → Task 6,8,10 (CV havuzu), Task 11,12,14 (eşleştirme), Task 15 (chat) ile karşılanıyor. §2 (stack kararı) → Global Constraints ve Tech Stack başlığında yansıtıldı. §3 (mimari) → dosya yapısı ve Task 1-3. §4 (veri modeli) → Task 4,10,11,14,15 ile birebir örtüşüyor. §5 (akışlar) → Task 6-15. §6 (LLM soyutlama) → Task 7,13,21. §7 (frontend) → Task 16-19. §8 (hata yönetimi/test) → Task 20 ve her task'taki TDD döngüsü. §9 (öğrenme sıralaması) → task sırası spec'teki 10 adımla birebir uyumlu.
- **Placeholder taraması:** Tüm adımlarda somut kod/komut var, "TBD"/"sonra eklenir" yok (Task 21'deki vector dimension migration'ı bilinçli olarak dokümante edilmiş bir sınırlama, placeholder değil).
- **Tip tutarlılığı:** `IEmbeddingProvider.EmbedAsync(string) : Task<float[]>` Task 7'de tanımlandı, Task 8/10/11/12/15/21'de aynı imza kullanıldı. `IChatProvider.CompleteAsync(string, List<(string,string)>, string) : Task<string>` Task 13'te tanımlandı, Task 14/15/21'de aynı imza kullanıldı. `MatchingService` constructor'ı Task 12'de `(CvRagDbContext)`, Task 14'te `(CvRagDbContext, IChatProvider)` olarak güncellendi — Task 14 Step 5 dosyanın tamamını yeniden yazdığı için tutarlı.
