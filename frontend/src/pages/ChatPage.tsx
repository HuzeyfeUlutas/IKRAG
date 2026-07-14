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
