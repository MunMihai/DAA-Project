/**
 * App.tsx / router — integrare completă
 *
 * Instalează dependența SignalR:
 *   npm install @microsoft/signalr
 *
 * Structura rutelor:
 *
 *   /login                    → LoginPage
 *   /signup                   → SignupPage
 *   /app (ProtectedRoute)
 *     /app                    → DashboardPage
 *     /app/quizzes            → QuizzesPage          ← actualizat
 *     /app/live               → JoinLivePage         ← NOU (studenți)
 *     /app/live/join/:code    → JoinLivePage         ← NOU (cu cod pre-completat)
 *     /app/live/host          → AdminLivePage        ← NOU (admin, fără sesiune)
 *     /app/live/host/:code    → AdminLivePage        ← NOU (admin, sesiune activă)
 *     /app/contests           → ContestsPage
 *     /app/submissions        → SubmissionsPage
 *     /app/admin/quizzes      → AdminQuizzesPage
 *     /app/admin/quizzes/:id  → AdminQuizEditorPage
 */

import { Navigate, Outlet, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import { AppShell } from "./layout/AppShell";

import { LoginPage } from "./pages/auth/LoginPage";
import { SignupPage } from "./pages/auth/SignupPage";
import { DashboardPage } from "./pages/DashboardPage";
import { AdminQuizzesPage } from "./pages/admin/AdminQuizzesPage";
import { AdminQuizEditorPage } from "./pages/admin/AdminQuizEditorPage";

// Optional: guard rute admin
import { useAuth } from "./auth/AuthContext";
import { QuizzesPage } from "./pages/app/QuizzesPage.tsx";
import { JoinLivePage } from "./pages/app/JoinLivePage.tsx";
import { AdminLivePage } from "./pages/admin/AdminLivePage.tsx";
import { ContestsPage } from "./pages/app/ContestsPage.tsx";
import { SubmissionsPage } from "./pages/app/SubmissionsPage.tsx";
import { applyTheme, getInitialTheme } from "./theme.ts";

function AdminRoute() {
    const { isAdmin, isReady } = useAuth();
    if (!isReady) return null;
    if (!isAdmin) return <Navigate to="/app" replace />;
    return <Outlet />;
}

export default function App() {
    applyTheme(getInitialTheme());
    return (
        <Routes>
            {/* Public */}
            <Route path="/login" element={<LoginPage />} />
            <Route path="/signup" element={<SignupPage />} />
            <Route path="/" element={<Navigate to="/app" replace />} />

            {/* Protected */}
            <Route element={<ProtectedRoute />}>
                <Route element={<AppShell><Outlet /></AppShell>}>

                    {/* Dashboard */}
                    <Route path="/app" element={<DashboardPage />} />

                    {/* Quizuri */}
                    <Route path="/app/quizzes" element={<QuizzesPage />} />

                    {/* Live — studenți */}
                    <Route path="/app/live" element={<JoinLivePage />} />
                    <Route path="/app/live/join/:code" element={<JoinLivePage />} />

                    {/* Live — admin/profesor */}
                    {/*<Route element={<AdminRoute />}>*/}
                    <Route path="/app/live/host" element={<AdminLivePage />} />
                    <Route path="/app/live/host/:code" element={<AdminLivePage />} />
                    {/*</Route>*/}

                    {/* Alte pagini */}
                    <Route path="/app/contests" element={<ContestsPage />} />
                    <Route path="/app/submissions" element={<SubmissionsPage />} />

                    {/* Admin */}
                    {/*<Route element={<AdminRoute />}>*/}
                    <Route path="/app/admin/quizzes" element={<AdminQuizzesPage />} />
                    <Route path="/app/admin/quizzes/:id" element={<AdminQuizEditorPage />} />
                    {/*</Route>*/}

                </Route>
            </Route>

            {/* Fallback */}
            <Route path="*" element={<Navigate to="/app" replace />} />
        </Routes>
    );
}