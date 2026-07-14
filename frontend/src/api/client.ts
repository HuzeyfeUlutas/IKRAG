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
