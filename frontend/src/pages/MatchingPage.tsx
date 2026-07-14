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
