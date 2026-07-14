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
