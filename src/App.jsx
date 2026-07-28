import { useState } from "react";
import { Routes, Route, Link, useLocation } from "react-router-dom";

import Nav from "./components/Nav";
import Footer from "./components/Footer";
import SplashIntro from "./components/SplashIntro";

import Home from "./pages/Home";
import Report from "./pages/Report";
import ReportLost from "./pages/ReportLost";
import ReportFound from "./pages/ReportFound";
import Browse from "./pages/Browse";
import SmartSearch from "./pages/SmartSearch";
import Dashboard from "./pages/Dashboard";
import Match from "./pages/Match";
import Login from "./pages/auth/Login";
import Register from "./pages/auth/Register";
import ForgotPassword from "./pages/auth/ForgotPassword";
import VerifyOtp from "./pages/auth/VerifyOtp";
import NotificationComposer from "./pages/admin/NotificationComposer";
import RequireAuth from "./components/RequireAuth";

function NotFound() {
  return (
    <div className="flex min-h-[70vh] items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-7xl font-bold text-foreground">404</h1>

        <h2 className="mt-4 text-xl font-semibold text-foreground">
          Page not found
        </h2>

        <p className="mt-2 text-sm text-muted-foreground">
          The page you're looking for doesn't exist or has been moved.
        </p>

        <Link
          to="/"
          className="mt-6 inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition-colors hover:opacity-90"
        >
          Go home
        </Link>
      </div>
    </div>
  );
}

export default function App() {
  const location = useLocation();
  const isAuthRoute = location.pathname.startsWith("/auth/");

  const [showSplash, setShowSplash] = useState(
    () => typeof window !== "undefined" && !window.sessionStorage.getItem("luqya-intro-seen")
  );

  function dismissSplash() {
    window.sessionStorage.setItem("luqya-intro-seen", "1");
    setShowSplash(false);
  }

  return (
    <div className="min-h-screen flex flex-col bg-background text-foreground">
      {showSplash && <SplashIntro onDone={dismissSplash} />}

      {!isAuthRoute && <Nav />}

      <main className="flex-1">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/report" element={<Report />} />
          <Route path="/report/lost" element={<ReportLost />} />
          <Route path="/report/found" element={<ReportFound />} />
          <Route path="/browse" element={<Browse />} />
          <Route path="/search" element={<SmartSearch />} />
          <Route
            path="/dashboard"
            element={
              <RequireAuth>
                <Dashboard />
              </RequireAuth>
            }
          />
          <Route path="/match/:id" element={<Match />} />

          <Route path="/auth/login" element={<Login />} />
          <Route path="/auth/register" element={<Register />} />
          <Route path="/auth/forgot-password" element={<ForgotPassword />} />
          <Route path="/auth/verify" element={<VerifyOtp />} />

          <Route path="/admin/notify" element={<NotificationComposer />} />

          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>

      {!isAuthRoute && <Footer />}
    </div>
  );
}