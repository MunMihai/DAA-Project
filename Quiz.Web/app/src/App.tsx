import { Navigate, Outlet, Route, Routes } from "react-router-dom";
import { LoginPage } from "./pages/auth/LoginPage.tsx";
import { SignupPage } from "./pages/auth/SignupPage.tsx";
import { ProtectedRoute } from "./auth/ProtectedRoute.tsx";
import { DashboardPage } from "./pages/DashboardPage.tsx";
import { AppShell } from "./layout/AppShell.tsx";
import { QuizzesPage } from "./pages/app/QuizzesPage.tsx";
import { ContestsPage } from "./pages/app/ContestsPage.tsx";
import { SubmissionsPage } from "./pages/app/SubmissionsPage.tsx";
import { AdminQuizzesPage } from "./pages/admin/AdminQuizzesPage.tsx";
import { AdminQuizEditorPage } from "./pages/admin/AdminQuizEditorPage.tsx";
import { applyTheme, getInitialTheme } from "./theme.ts";

export function AppLayout() {
    return (
        <AppShell>
            <Outlet />
        </AppShell>
    );
}

export default function App() {
    applyTheme(getInitialTheme());
    return (
        <Routes>
            <Route path="/" element={<Navigate to="/login" replace />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/signup" element={<SignupPage />} />

            {/* protected app area */}
            <Route element={<ProtectedRoute />}>
                <Route path="/app" element={<AppLayout />}>
                    <Route index element={<DashboardPage />} />
                    <Route path="quizzes" element={<QuizzesPage />} />
                    <Route path="contests" element={<ContestsPage />} />
                    <Route path="submissions" element={<SubmissionsPage />} />
                    <Route path="admin/quizzes" element={<AdminQuizzesPage />} />
                    <Route path="admin/quizzes/:id" element={<AdminQuizEditorPage />} />
                </Route>
            </Route>

            <Route path="*" element={<Navigate to="/app" replace />} />
        </Routes>
    );
}