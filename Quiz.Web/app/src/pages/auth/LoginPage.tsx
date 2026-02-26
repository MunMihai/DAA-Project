import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { AuthShell } from "../../components/AuthShell.tsx";

export function LoginPage() {
    const [loading, setLoading] = useState(false);
    const [showPw, setShowPw] = useState(false);

    const demoEmail = useMemo(() => "student@example.com", []);

    const onSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        // TODO: call API
        setTimeout(() => setLoading(false), 600);
    };

    return (
        <AuthShell title="Conectează-te" subtitle="Intră în cont pentru a continua quizurile și concursurile.">
            <form onSubmit={onSubmit} className="space-y-4">
                <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                        Email
                    </label>
                    <input
                        type="email"
                        defaultValue={demoEmail}
                        className="w-full rounded-2xl border border-slate-900/10 bg-white/80 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-500/50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        placeholder="nume@exemplu.md"
                        autoComplete="email"
                        required
                    />
                </div>

                <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                        Parolă
                    </label>
                    <div className="relative">
                        <input
                            type={showPw ? "text" : "password"}
                            className="w-full rounded-2xl border border-slate-900/10 bg-white/80 px-4 py-3 pr-12 text-sm text-slate-900 shadow-sm outline-none placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-500/50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                            placeholder="••••••••"
                            autoComplete="current-password"
                            required
                        />
                        <button
                            type="button"
                            onClick={() => setShowPw((v) => !v)}
                            className="absolute inset-y-0 right-2 my-2 rounded-xl px-3 text-xs font-semibold text-slate-700 hover:bg-slate-900/5 dark:text-slate-200 dark:hover:bg-white/5"
                        >
                            {showPw ? "Ascunde" : "Arată"}
                        </button>
                    </div>
                </div>

                <div className="flex items-center justify-between">
                    <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                        <input
                            type="checkbox"
                            className="h-4 w-4 rounded border-slate-900/20 bg-white/80 text-indigo-600 focus:ring-indigo-500/50 dark:border-white/20 dark:bg-slate-950/40"
                        />
                        Ține-mă minte
                    </label>

                    <button
                        type="button"
                        className="text-sm font-semibold text-indigo-700 hover:text-indigo-800 dark:text-indigo-300 dark:hover:text-indigo-200"
                    >
                        Ai uitat parola?
                    </button>
                </div>

                <button
                    type="submit"
                    disabled={loading}
                    className="inline-flex w-full items-center justify-center rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500/60 disabled:opacity-60 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100"
                >
                    {loading ? "Se conectează..." : "Login"}
                </button>

                <div className="relative py-1">
                    <div className="absolute inset-0 flex items-center">
                        <div className="h-px w-full bg-slate-900/10 dark:bg-white/10" />
                    </div>
                    <div className="relative flex justify-center">
            <span className="bg-white/70 px-3 text-xs text-slate-600 dark:bg-slate-900/50 dark:text-slate-400">
              sau
            </span>
                    </div>
                </div>

                <button
                    type="button"
                    className="inline-flex w-full items-center justify-center gap-2 rounded-2xl border border-slate-900/10 bg-white/70 px-4 py-3 text-sm font-semibold text-slate-800 shadow-sm hover:bg-white/90 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                >
                    Continuă cu Google
                </button>

                <p className="pt-2 text-center text-sm text-slate-700 dark:text-slate-300">
                    Nu ai cont?{" "}
                    <Link
                        to="/signup"
                        className="font-semibold text-indigo-700 hover:text-indigo-800 dark:text-indigo-300 dark:hover:text-indigo-200"
                    >
                        Creează cont
                    </Link>
                </p>
            </form>
        </AuthShell>
    );
}