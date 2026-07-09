# CV İnceleme Uygulaması — Tasarım Dokümanı

**Tarih:** 2026-07-09
**Amaç:** Öğrenme odaklı proje. Ana hedef RAG (Retrieval-Augmented Generation) ve vector DB kavramlarını uygulamalı öğrenmek. Stack: .NET (backend) + React (frontend).

## 1. Kapsam

Uygulama üç ana yetenek sunar:

1. **CV Havuzu** — PDF formatında CV'ler yüklenir, sisteme kaydedilir.
2. **İlan Eşleştirme** — Bir iş ilanı metni (LinkedIn'den kopyala-yapıştır) girilir, havuzdaki CV'ler bu ilana göre bulunur ve puanlanır.
3. **CV Chat** — Havuzdan seçilen tek bir CV hakkında doğal dilde soru-cevap yapılabilir.

**Kapsam dışı (bilinçli olarak):**
- LinkedIn scraping / otomatik ilan çekme — sadece metin yapıştırma.
- Kullanıcı girişi / yetkilendirme — tek kullanıcılı, auth yok.
- Mikroservis mimarisi — tek .NET Web API projesi (monolith).
- Kapsamlı otomatik test paketi — sadece kritik iş mantığı için birim testleri.

## 2. Teknoloji Stack Kararı

Kullanıcının mevcut uzmanlığı .NET + React ekosisteminde. Alternatif olarak Python (FastAPI + LangChain/LlamaIndex) ve Node/TypeScript (Next.js + LangChain.js) değerlendirildi:

- **Python**: En olgun RAG ekosistemi, en çok tutorial, ama yeni dil öğrenme yükü RAG öğrenimine odaklanmayı böler.
- **Node/TS**: Tek dilde frontend+backend, ama LangChain.js daha az olgun, yine yeni ekosistem öğrenme maliyeti var.
- **.NET + React (seçilen)**: Dil/framework mekaniği zaten bilindiği için tüm zihinsel enerji RAG kavramlarına (chunking, embedding, retrieval, prompt injection) gidiyor. Microsoft'un **Semantic Kernel** framework'ü Ollama ve OpenAI için resmi connector'lara, pgvector connector'üne sahip — RAG kavramları dilden bağımsız olduğu için stack değişikliği öğrenim değeri katmıyor, sadece maliyet ekliyor.

**Karar: .NET + React + Semantic Kernel.**

## 3. Genel Mimari

```
┌─────────────┐      REST API       ┌──────────────────────┐
│   React     │ ──────────────────▶ │   .NET Web API        │
│  (frontend) │ ◀────────────────── │  (ASP.NET Core)        │
└─────────────┘                     └──────────┬────────────┘
                                                │
                        ┌───────────────────────┼───────────────────────┐
                        │                        │                       │
                ┌───────▼────────┐    ┌──────────▼──────────┐  ┌────────▼────────┐
                │  PostgreSQL     │    │  ILlmProvider        │  │  PdfPig          │
                │  + pgvector     │    │  (Ollama / OpenAI)   │  │  (PDF metin      │
                │  (CV, ilan,     │    │  interface + DI      │  │   çıkarma)       │
                │   chunk, embed) │    └──────────────────────┘  └──────────────────┘
                └─────────────────┘
```

- **React frontend** — CV yükleme, ilan yapıştırma, sonuç listesi, chat ekranı. Vite ile plain React, React Router, Tailwind CSS, sayfa başına `useState` + `fetch` (global state yönetim kütüphanesi yok).
- **.NET Web API** — tek proje, monolith, tüm iş mantığı burada.
- **PostgreSQL + pgvector** — hem ilişkisel veri (CV, ilan, chunk) hem embedding vektörleri tek DB'de, EF Core + Npgsql ile erişim.
- **`ILlmProvider` soyutlaması** — Ollama ile başlar, `appsettings.json` üzerinden OpenAI'a geçilebilir.
- **Docker Compose** — PostgreSQL (pgvector extension'lı image) + Ollama container, tek komutla (`docker compose up`) local ortam hazır olur.

## 4. Veri Modeli

```
JobPosting
├── Id, RawText, CreatedAt
├── EmbeddingVector (vector) — tüm ilan metninin embedding'i (havuz eşleştirme için)

CvDocument
├── Id, FileName, RawText, CreatedAt
├── EmbeddingVector (vector) — tüm CV'nin embedding'i (havuz eşleştirme için)

CvChunk
├── Id, CvDocumentId (FK)
├── ChunkText, ChunkIndex
├── EmbeddingVector (vector) — chat/QA retrieval için

MatchResult
├── Id, JobPostingId (FK), CvDocumentId (FK)
├── SimilarityScore (cosine, aşama 1)
├── LlmScore (0-100, aşama 2), LlmReasoning (text)
├── CreatedAt

ChatMessage
├── Id, CvDocumentId (FK)
├── Role (user/assistant), Content, CreatedAt
```

`CvDocument.EmbeddingVector` (tüm CV, eşleştirme için) ile `CvChunk.EmbeddingVector` (parça parça, chat retrieval için) bilinçli olarak ayrı tutulur — farklı amaçlara hizmet ederler.

## 5. Backend Akışları

**a) CV yükleme**
`POST /api/cvs` → PDF al → PdfPig ile metin çıkar → `CvDocument` kaydet → tüm metni embed et (`IEmbeddingProvider`) → chunk'lara böl (~500 karakter, overlap'li) → her chunk'ı embed et → `CvChunk` kayıtları oluştur.

**b) İlan girme + havuz eşleştirme**
`POST /api/job-postings` → metni kaydet + embed et.
`POST /api/job-postings/{id}/match` →
1. pgvector ile `CvDocument.EmbeddingVector` üzerinde cosine similarity, top N (örn. 10) getir.
2. Bu N CV için LLM'e (ilan metni + CV metni) prompt gönder → 0-100 puan + kısa gerekçe iste.
3. `MatchResult` kayıtlarını oluştur, `LlmScore`'a göre sıralı döndür.

**c) CV chat**
`POST /api/cvs/{id}/chat` (mesaj body'de) →
1. Soruyu embed et.
2. O CV'ye ait `CvChunk`'lar arasında cosine similarity ile top K (örn. 3-4) chunk getir.
3. LLM'e sistem prompt + bulunan chunk'lar + soru geçmişi + yeni soru gönder → cevap üret.
4. `ChatMessage` (user + assistant) kaydet, cevabı döndür.

## 6. LLM/Embedding Soyutlaması

```csharp
public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text);
}

public interface IChatProvider
{
    Task<string> CompleteAsync(string systemPrompt, IEnumerable<ChatMessage> history, string userMessage);
}
```

- **Ollama implementasyonu**: yerel HTTP API'ye (`http://localhost:11434`) istek atar. Embedding modeli: `nomic-embed-text`. Chat modeli: `llama3.1` (veya `qwen2.5`).
- **OpenAI implementasyonu**: `text-embedding-3-small` + `gpt-4o-mini`. Aynı interface'i implement eder.
- **Seçim**: `appsettings.json`'da `"LlmProvider": "Ollama" | "OpenAI"` — DI container'da bu değere göre hangi implementasyon enjekte edileceği belirlenir (basit factory/switch).

## 7. Frontend

- **Sayfa 1 — CV Havuzu**: PDF yükleme formu, yüklenen CV'lerin listesi.
- **Sayfa 2 — İlan & Eşleştirme**: İlan metni textarea'sı, "Eşleştir" butonu, sonuç listesi (CV adı, puan, LLM gerekçesi), puana göre sıralı.
- **Sayfa 3 — CV Chat**: Havuzdan CV seçimi, basit chat arayüzü (mesaj geçmişi + input).
- **Teknik seçimler**: Vite + React, React Router, Tailwind CSS, sayfa başına local state.

## 8. Hata Yönetimi & Test

- **Hata yönetimi**: Global exception middleware (.NET) ile tutarlı JSON hata formatı. LLM/Ollama çağrısı başarısız olursa kullanıcıya anlamlı hata mesajı gösterilir.
- **Test**: Ağır test altyapısı yok. Kritik noktalarda (chunking mantığı, cosine similarity hesaplama, provider seçimi) birim testleri; embedding/LLM çağrıları fake/mock provider ile test edilir.
- **Ortam**: Docker Compose ile PostgreSQL (pgvector) + Ollama container, `docker compose up` ile local ortam hazır.

## 9. Öğrenme Yaklaşımı

Kullanıcı bu projeyi küçük, anlaşılır adımlarla ilerleterek öğrenmek istiyor. Implementasyon planı bu nedenle şu sırayla küçük parçalara bölünecek (writing-plans aşamasında detaylandırılacak):

1. Altyapı: Docker Compose (Postgres+pgvector, Ollama), .NET projesi iskeleti, React projesi iskeleti.
2. CV yükleme + PDF metin çıkarma (embedding olmadan, sadece kayıt).
3. Embedding provider soyutlaması (Ollama implementasyonu) + CV embedding kaydı.
4. Chunking + chunk embedding.
5. İlan girme + embedding.
6. Vector similarity ile top-N eşleştirme (LLM yok, sadece cosine similarity).
7. LLM ile yeniden sıralama (puan + gerekçe).
8. CV chat (retrieval + generation).
9. Frontend entegrasyonu (üç sayfa, adım adım backend'e bağlanır).
10. (Opsiyonel, sona bırakılır) OpenAI provider implementasyonu ve geçiş testi.

Her adım kendi başına çalışır ve test edilebilir durumda olacak şekilde tasarlanmıştır.
