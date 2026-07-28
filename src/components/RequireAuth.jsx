import { Navigate, useLocation } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { useAuth } from "../lib/useAuth";

// Guards frontend-private pages (Dashboard, Profile, My Reports, ...).
// The backend itself doesn't enforce auth on most of these endpoints
// (see audit), but the brief is explicit: private account UI should stay
// login-gated in the frontend regardless of what the API would allow.
export default function RequireAuth({ children }) {
  const { profile, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="flex items-center justify-center py-32 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" />
      </div>
    );
  }

  if (!profile) {
    return <Navigate to="/auth/login" state={{ from: location.pathname }} replace />;
  }

  return children;
}
