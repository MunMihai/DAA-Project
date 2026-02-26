import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "./AuthContext";

export function ProtectedRoute() {
    const { isReady, user } = useAuth();

    if (!isReady) {
        return (
            <div className="min-h-screen grid place-items-center text-sm text-slate-600 dark:text-slate-400">
                Loading...
            </div>
        );
    }

    if (!user) return <Navigate to="/login" replace />;

    return <Outlet />;
}