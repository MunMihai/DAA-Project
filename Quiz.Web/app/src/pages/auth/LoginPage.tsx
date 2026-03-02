import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext.tsx";
import { AuthShell } from "../../components/AuthShell.tsx";
import { toast } from "react-toastify";
import { useApi } from "../../api/axios.tsx";

export function LoginPage() {
    const { login } = useAuth();
    const navigate = useNavigate();
    const api = useApi();

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [showPw, setShowPw] = useState(false);

    const [emailStr, setEmailStr] = useState("");
    const [passwordStr, setPasswordStr] = useState("");

    const [seeding, setSeeding] = useState(false);

    const handleRunSeed = async () => {
        setSeeding(true);
        try {
            const res = await api.post("/identity/auth/run-seed");
            toast.success(res.data.message || "Baza de date a fost populată cu succes!");
        } catch (err) {
            const error = err as any;
            toast.error(error?.response?.data?.message || "Eroare la rularea seed-ului.");
        } finally {
            setSeeding(false);
        }
    };

    const handleAutocomplete = (role: string) => {
        setEmailStr(`${role.toLowerCase()}@exemplu.md`);
        setPasswordStr("Password123!");
    };

    const onSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        setError(null);

        const email = emailStr.trim();
        const password = passwordStr.trim();

        if (!email || !password) {
            setError("Email și parola sunt obligatorii.");
            return;
        }

        setLoading(true);
        try {
            await login(email, password);
            navigate("/app");
        } catch (err) {
            const error = err as any;
            setError(
                error?.response?.data?.message ??
                "Email sau parolă greșită. Încearcă din nou."
            );
        } finally {
            setLoading(false);
        }
    };

    return (
        <AuthShell
            title="Conectează-te"
            subtitle="Intră în cont pentru a continua quizurile și concursurile."
        >
            <form onSubmit={onSubmit} className="space-y-4">
                <div className="flex justify-between items-center mb-4">
                    <span className="text-sm text-slate-500 font-medium">Bază de date:</span>
                    <button
                        type="button"
                        onClick={handleRunSeed}
                        disabled={seeding}
                        className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-300 disabled:opacity-50 transition-colors"
                    >
                        {seeding ? "Se populează..." : "▶ Run Seed"}
                    </button>
                </div>

                <div className="flex gap-2 mb-4 justify-center flex-wrap">
                    <button type="button" onClick={() => handleAutocomplete("Admin")} className="text-xs px-2 py-1 rounded-md bg-slate-100 hover:bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300">Auto Admin</button>
                    <button type="button" onClick={() => handleAutocomplete("Teacher")} className="text-xs px-2 py-1 rounded-md bg-slate-100 hover:bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300">Auto Teacher</button>
                    <button type="button" onClick={() => handleAutocomplete("Student")} className="text-xs px-2 py-1 rounded-md bg-slate-100 hover:bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300">Auto Student</button>
                </div>

                <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                        Email
                    </label>
                    <input
                        name="email"
                        type="email"
                        required
                        value={emailStr}
                        onChange={(e) => setEmailStr(e.target.value)}
                        className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        placeholder="student@exemplu.md"
                        autoComplete="email"
                    />
                </div>

                <div>
                    <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                        Parolă
                    </label>
                    <div className="relative">
                        <input
                            name="password"
                            type={showPw ? "text" : "password"}
                            required
                            value={passwordStr}
                            onChange={(e) => setPasswordStr(e.target.value)}
                            className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 pr-12 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                            placeholder="••••••••"
                            autoComplete="current-password"
                        />
                        <button
                            type="button"
                            onClick={() => setShowPw((v) => !v)}
                            className="absolute inset-y-0 right-2 my-2 rounded-xl px-3 text-xs font-semibold text-slate-700 hover:bg-slate-200 dark:text-slate-200 dark:hover:bg-white/10"
                        >
                            {showPw ? "Ascunde" : "Arată"}
                        </button>
                    </div>
                </div>

                <div className="flex items-center justify-between">
                    <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                        <input
                            name="remember"
                            type="checkbox"
                            className="h-4 w-4 rounded border-slate-300 bg-slate-50 text-indigo-600 focus:ring-indigo-500/40 dark:border-white/20 dark:bg-slate-950/40"
                        />
                        Ține-mă minte
                    </label>

                    <button
                        type="button"
                        className="text-sm font-semibold text-indigo-700 hover:text-indigo-800 dark:text-indigo-300 dark:hover:text-indigo-200"
                        onClick={() => setError("TODO: implement reset password")}
                    >
                        Ai uitat parola?
                    </button>
                </div>

                {error && (
                    <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                        {error}
                    </div>
                )}

                <button
                    type="submit"
                    disabled={loading}
                    className="inline-flex w-full items-center justify-center rounded-2xl bg-indigo-600 px-4 py-3 text-sm font-semibold text-white shadow-sm hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-indigo-500/60 disabled:opacity-60 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                >
                    {loading ? "Se conectează..." : "Login"}
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