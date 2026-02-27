import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { quizApi, type Quiz } from "../../api/quizApi";
import { liveSessionApi } from "../../api/liveSessionApi";
import { useApi } from "../../api/axios";
import { useAuth } from "../../auth/AuthContext";

function cn(...xs: (string | false | null | undefined)[]) {
    return xs.filter(Boolean).join(" ");
}

function TagBadge({ text }: { text: string }) {
    return (
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">
            #{text}
        </span>
    );
}

export function QuizzesPage() {
    const api = useApi();
    const { isAdmin } = useAuth();
    const qapi = useMemo(() => quizApi(api), [api]);
    const lsApi = useMemo(() => liveSessionApi(api), [api]);
    const nav = useNavigate();

    const [quizzes, setQuizzes] = useState<Quiz[]>([]);
    const [loading, setLoading] = useState(true);
    const [err, setErr] = useState<string | null>(null);
    const [launching, setLaunching] = useState<string | null>(null);
    const [search, setSearch] = useState("");

    useEffect(() => {
        qapi.list({ status: 1 })
            .then(setQuizzes)
            .catch(() => setErr("Nu pot încărca quizurile."))
            .finally(() => setLoading(false));
    }, [qapi]);

    const filtered = useMemo(() => {
        const q = search.trim().toLowerCase();
        if (!q) return quizzes;
        return quizzes.filter(x =>
            x.title.toLowerCase().includes(q) ||
            (x.tags ?? []).some(t => t.toLowerCase().includes(q))
        );
    }, [quizzes, search]);

    const startLive = async (quizId: string) => {
        setLaunching(quizId);
        try {
            const res = await lsApi.create({ quizId });
            nav(`/app/live/host/${res.sessionCode}`);
        } catch (e: any) {
            setErr(e?.response?.data?.message ?? "Nu pot crea sesiunea live.");
            setLaunching(null);
        }
    };

    return (
        <div className="space-y-6">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
                    <div>
                        <h1 className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">Quizuri</h1>
                        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                            {isAdmin
                                ? "Lansează sesiuni live multiplayer pentru studenți."
                                : "Intră într-o sesiune live cu codul de la profesor."}
                        </p>
                    </div>

                    {/* Student join shortcut */}
                    {!isAdmin && (
                        <button
                            onClick={() => nav("/app/live")}
                            className="inline-flex items-center gap-2 rounded-2xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-indigo-700 dark:bg-indigo-500"
                        >
                            <span className="h-2 w-2 animate-pulse rounded-full bg-white/60" />
                            Intră cu cod
                        </button>
                    )}
                </div>

                <div className="mt-4">
                    <input
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        placeholder="Caută după titlu sau tag..."
                        className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                    />
                </div>
            </div>

            {err && (
                <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                    {err}
                </div>
            )}

            {loading ? (
                <div className="rounded-3xl border border-slate-900/10 bg-white p-12 text-center dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mx-auto h-8 w-8 animate-spin rounded-full border-4 border-indigo-200 border-t-indigo-600" />
                </div>
            ) : (
                <div className="rounded-3xl border border-slate-900/10 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="border-b border-slate-900/10 px-6 py-4 text-sm font-semibold text-slate-500 dark:border-white/10 dark:text-slate-400">
                        {filtered.length} {filtered.length === 1 ? "quiz" : "quizuri"} publicate
                    </div>

                    {filtered.length === 0 && (
                        <div className="px-6 py-14 text-center text-sm text-slate-400 dark:text-slate-500">
                            Niciun quiz găsit.
                        </div>
                    )}

                    <div className="divide-y divide-slate-900/5 dark:divide-white/5">
                        {filtered.map(q => (
                            <div key={q.id}
                                 className="flex flex-col gap-3 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
                                <div className="min-w-0">
                                    <div className="flex flex-wrap items-center gap-2">
                                        <span className="text-sm font-semibold text-slate-900 dark:text-slate-100 truncate">
                                            {q.title}
                                        </span>
                                    </div>
                                    {q.description && (
                                        <p className="mt-0.5 text-xs text-slate-400 dark:text-slate-500 line-clamp-1">
                                            {q.description}
                                        </p>
                                    )}
                                    <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                                        <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-semibold text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-400">
                                            {q.questions?.length ?? 0} întrebări
                                        </span>
                                        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">
                                            {q.timeLimitSeconds}s/întrebare
                                        </span>
                                        {(q.tags ?? []).slice(0, 3).map(t => <TagBadge key={t} text={t} />)}
                                    </div>
                                </div>

                                <div className="flex shrink-0 gap-2">
                                    {isAdmin && (
                                        <button
                                            onClick={() => startLive(q.id)}
                                            disabled={launching === q.id}
                                            className={cn(
                                                "inline-flex items-center gap-1.5 rounded-xl px-4 py-2 text-sm font-semibold transition",
                                                "bg-red-50 text-red-700 border border-red-200 hover:bg-red-100",
                                                "dark:border-red-800/40 dark:bg-red-900/20 dark:text-red-300 dark:hover:bg-red-900/30",
                                                "disabled:opacity-50 disabled:cursor-not-allowed"
                                            )}
                                        >
                                            {launching === q.id ? (
                                                <>
                                                    <span className="h-3 w-3 animate-spin rounded-full border-2 border-red-400 border-t-transparent" />
                                                    Lansare...
                                                </>
                                            ) : (
                                                <>
                                                    <span className="h-2 w-2 animate-pulse rounded-full bg-red-500" />
                                                    Live
                                                </>
                                            )}
                                        </button>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* Student CTA */}
            {!isAdmin && (
                <div className="rounded-3xl border border-indigo-200 bg-indigo-50 p-6 dark:border-indigo-500/20 dark:bg-indigo-500/5">
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                        <div>
                            <div className="text-sm font-semibold text-indigo-800 dark:text-indigo-300">
                                Ai primit un cod de la profesor?
                            </div>
                            <p className="mt-0.5 text-xs text-indigo-600/80 dark:text-indigo-400/80">
                                Intră în sesiunea live și concurează cu colegii în timp real.
                            </p>
                        </div>
                        <button
                            onClick={() => nav("/app/live")}
                            className="shrink-0 rounded-2xl bg-indigo-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-indigo-700 dark:bg-indigo-500 transition"
                        >
                            Intră cu cod →
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}