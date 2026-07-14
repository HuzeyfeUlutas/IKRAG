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
