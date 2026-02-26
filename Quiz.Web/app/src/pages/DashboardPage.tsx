import { useAuth } from "../auth/AuthContext.tsx";

export function DashboardPage() {
    const { user, logout } = useAuth();

    return (
        <div className="min-h-screen px-4 py-10">
            <div className="mx-auto max-w-3xl rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="text-lg font-bold text-slate-900 dark:text-white">Dashboard</div>
                <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                    Autentificat ca: <span className="font-semibold">{user?.email}</span>
                </div>

                <button
                    onClick={logout}
                    className="mt-6 inline-flex items-center justify-center rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100"
                >
                    Logout
                </button>
            </div>
        </div>
    );
}