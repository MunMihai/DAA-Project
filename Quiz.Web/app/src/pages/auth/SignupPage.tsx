import { useState } from "react";
import { Link } from "react-router-dom";
import { AuthShell } from "../../components/AuthShell.tsx";

export function SignupPage() {
    const [loading, setLoading] = useState(false);
    const [showPw, setShowPw] = useState(false);

    const onSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true);
        // TODO: call API
        setTimeout(() => setLoading(false), 700);
    };

    return (
        <AuthShell title="Creează cont" subtitle="Începe cu quizuri și concursuri de programare.">
            <form onSubmit={onSubmit} className="space-y-4">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <div>
                        <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                            Prenume
                        </label>
                        <input
                            type="text"
                            className="w-full rounded-2xl border border-slate-900/10 bg-white/80 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-500/50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                            placeholder="Ana"
                            autoComplete="given-name"
                            required
                        />
                    </div>
                    <div>
                        <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                            Nume
                        </label>
                        <input
                            type="text"
                            className="w-full rounded-2xl border border-slate-900/10 bg-white/80 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-500/50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                            placeholder="Popescu"
                            autoComplete="family-name"
                            required
                        />
                    </div>
                </div>

                <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                        Email
                    </label>
                    <input
                        type="email"
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
                            placeholder="minim 8 caractere"
                            autoComplete="new-password"
                            minLength={8}
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
                    <p className="mt-2 text-xs text-slate-600 dark:text-slate-400">
                        Recomandat: literă mare, cifră, simbol.
                    </p>
                </div>

                <label className="flex items-start gap-2 rounded-2xl border border-slate-900/10 bg-white/60 p-3 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300">
                    <input
                        type="checkbox"
                        className="mt-1 h-4 w-4 rounded border-slate-900/20 bg-white/80 text-indigo-600 focus:ring-indigo-500/50 dark:border-white/20 dark:bg-slate-950/40"
                        required
                    />
                    <span>
            Sunt de acord cu <span className="font-semibold text-slate-800 dark:text-slate-100">Termenii</span> și{" "}
                        <span className="font-semibold text-slate-800 dark:text-slate-100">Politica</span>.
          </span>
                </label>

                <button
                    type="submit"
                    disabled={loading}
                    className="inline-flex w-full items-center justify-center rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-indigo-500/60 disabled:opacity-60 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100"
                >
                    {loading ? "Se creează..." : "Sign up"}
                </button>

                <p className="pt-2 text-center text-sm text-slate-700 dark:text-slate-300">
                    Ai deja cont?{" "}
                    <Link
                        to="/login"
                        className="font-semibold text-indigo-700 hover:text-indigo-800 dark:text-indigo-300 dark:hover:text-indigo-200"
                    >
                        Login
                    </Link>
                </p>
            </form>
        </AuthShell>
    );
}