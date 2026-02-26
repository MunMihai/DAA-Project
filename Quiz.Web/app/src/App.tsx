import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./pages/auth/LoginPage.tsx";
import { SignupPage } from "./pages/auth/SignupPage.tsx";
import { ProtectedRoute } from "./auth/ProtectedRoute.tsx";
import { DashboardPage } from "./pages/DashboardPage.tsx";


export default function App() {
    return (
        <Routes>
            <Route path="/" element={<Navigate to="/login" replace />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/signup" element={<SignupPage />} />

            <Route element={<ProtectedRoute />}>
                <Route path="/app" element={<DashboardPage />} />
            </Route>
        </Routes>
    );
}