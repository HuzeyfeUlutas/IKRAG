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
