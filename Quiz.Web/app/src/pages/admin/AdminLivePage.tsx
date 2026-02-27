import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { quizApi, type Quiz } from "../../api/quizApi";
import { liveSessionApi } from "../../api/liveSessionApi";
import { useApi } from "../../api/axios";
import { useLiveSession, type LeaderboardEntry, type QuizQuestion } from "../../hooks/useLiveSession";

function cn(...xs: (string | false | null | undefined)[]) {
    return xs.filter(Boolean).join(" ");
}

// ── Countdown ─────────────────────────────────────────────────────────────────
function Countdown({ deadline, total }: { deadline: Date; total: number }) {
    const [left, setLeft] = useState(0);
    useEffect(() => {
        const t = setInterval(() => {
            setLeft(Math.max(0, Math.ceil((deadline.getTime() - Date.now()) / 1000)));
        }, 200);
        return () => clearInterval(t);
    }, [deadline]);

    const pct = total > 0 ? (left / total) * 100 : 0;
    const danger = pct < 25;
    const warn = pct < 50;

    return (
        <div className="flex items-center gap-3">
            <div className="relative h-14 w-14 shrink-0">
                <svg className="h-14 w-14 -rotate-90" viewBox="0 0 56 56">
                    <circle cx="28" cy="28" r="24" fill="none" stroke="currentColor"
                            strokeWidth="4" className="text-slate-200 dark:text-slate-700" />
                    <circle cx="28" cy="28" r="24" fill="none"
                            strokeWidth="4"
                            strokeLinecap="round"
                            strokeDasharray={`${2 * Math.PI * 24}`}
                            strokeDashoffset={`${2 * Math.PI * 24 * (1 - pct / 100)}`}
                            className={cn(
                                "transition-all duration-500",
                                danger ? "stroke-red-500" : warn ? "stroke-amber-500" : "stroke-emerald-500"
                            )} />
                </svg>
                <span className={cn(
                    "absolute inset-0 flex items-center justify-center text-sm font-black",
                    danger ? "text-red-600 dark:text-red-400" : "text-slate-700 dark:text-slate-200"
                )}>
                    {left}
                </span>
            </div>
            <div className="text-xs text-slate-500 dark:text-slate-400">secunde<br />rămase</div>
        </div>
    );
}

// ── Leaderboard sidebar ───────────────────────────────────────────────────────
function LeaderboardPanel({ entries, compact = false }: { entries: LeaderboardEntry[]; compact?: boolean }) {
    const medals = ["🥇", "🥈", "🥉"];
    if (entries.length === 0) {
        return <p className="text-sm text-slate-400 dark:text-slate-500 italic">Niciun punct încă.</p>;
    }
    return (
        <ol className="space-y-1.5">
            {entries.slice(0, compact ? 5 : 20).map((e, i) => (
                <li key={e.playerId} className={cn(
                    "flex items-center justify-between rounded-xl px-3 py-2 text-sm font-medium transition-all",
                    i === 0 && "bg-amber-50 text-amber-900 ring-1 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-200 dark:ring-amber-500/20",
                    i === 1 && "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
                    i === 2 && "bg-orange-50 text-orange-800 dark:bg-orange-500/10 dark:text-orange-200",
                    i > 2 && "text-slate-600 dark:text-slate-400",
                )}>
                    <span className="flex items-center gap-2 min-w-0">
                        <span className="w-6 shrink-0 text-center">{medals[i] ?? `${i + 1}`}</span>
                        <span className="truncate">{e.displayName}</span>
                    </span>
                    <span className="ml-2 shrink-0 font-bold tabular-nums">{e.score}p</span>
                </li>
            ))}
        </ol>
    );
}

// ── QR-code placeholder ───────────────────────────────────────────────────────
function SessionCodeDisplay({ code }: { code: string }) {
    const [copied, setCopied] = useState(false);
    const copy = () => {
        navigator.clipboard.writeText(code);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <div className="flex flex-col items-center gap-3">
            {/* Big code */}
            <div className="rounded-2xl bg-indigo-600 px-8 py-5 text-center dark:bg-indigo-500">
                <div className="text-xs font-semibold uppercase tracking-widest text-indigo-200">Cod sesiune</div>
                <div className="mt-1 font-mono text-5xl font-black tracking-widest text-white">{code}</div>
            </div>
            <button onClick={copy}
                    className="rounded-xl border border-slate-200 bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200">
                {copied ? "✓ Copiat!" : "Copiază codul"}
            </button>
            <p className="text-center text-xs text-slate-500 dark:text-slate-400">
                Studenții accesează <span className="font-semibold text-slate-700 dark:text-slate-200">/app/live</span> și introduc codul
            </p>
        </div>
    );
}

// ── Question view (host sees it without correct answers) ──────────────────────
function QuestionDisplay({ q, index, total }: { q: QuizQuestion; index: number; total: number }) {
    const typeLabel = ["Adevărat/Fals", "Alegere unică", "Alegere multiplă", "Răspuns text"][q.type];
    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between">
                <span className="rounded-full border border-indigo-200 bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-700 dark:border-indigo-500/30 dark:bg-indigo-500/10 dark:text-indigo-300">
                    {typeLabel}
                </span>
                <span className="text-sm font-semibold text-slate-500 dark:text-slate-400">
                    Întrebarea {index + 1} / {total} • {q.points} {q.points === 1 ? "punct" : "puncte"}
                </span>
            </div>

            <div className="rounded-2xl border border-slate-200 bg-slate-50 px-5 py-4 text-base font-medium leading-relaxed text-slate-900 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100">
                {q.prompt}
            </div>

            {q.options.length > 0 && (
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    {q.options.map((o, oi) => (
                        <div key={o.id} className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-900/40 dark:text-slate-300">
                            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-xs font-bold text-slate-600 dark:bg-slate-800 dark:text-slate-400">
                                {String.fromCharCode(65 + oi)}
                            </span>
                            {o.text}
                        </div>
                    ))}
                </div>
            )}

            {q.type === 0 && (
                <div className="flex gap-3">
                    {["Adevărat", "Fals"].map(l => (
                        <div key={l} className="flex-1 rounded-xl border border-slate-200 bg-white px-4 py-3 text-center text-sm font-semibold text-slate-700 dark:border-white/10 dark:bg-slate-900/40 dark:text-slate-300">
                            {l}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

// ── Step: pick quiz ───────────────────────────────────────────────────────────
function PickQuizStep({ onPick }: { onPick: (quiz: Quiz) => void }) {
    const api = useApi();
    const qapi = useMemo(() => quizApi(api), [api]);
    const [quizzes, setQuizzes] = useState<Quiz[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");

    useEffect(() => {
        qapi.list({ status: 1 }).then(setQuizzes).finally(() => setLoading(false));
    }, [qapi]);

    const filtered = quizzes.filter(q =>
        q.title.toLowerCase().includes(search.toLowerCase()) ||
        (q.tags ?? []).some(t => t.toLowerCase().includes(search.toLowerCase()))
    );

    return (
        <div className="space-y-6">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Lansează sesiune live
                </div>
                <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                    Alege un quiz publicat. Studenții vor face join cu un cod de 6 caractere.
                </p>
                <div className="mt-4">
                    <input
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        placeholder="Caută quiz..."
                        className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                    />
                </div>
            </div>

            {loading && (
                <div className="rounded-3xl border border-slate-900/10 bg-white p-10 text-center text-sm text-slate-500 dark:border-white/10 dark:bg-slate-900/55">
                    Se încarcă...
                </div>
            )}

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {filtered.map(q => (
                    <button
                        key={q.id}
                        onClick={() => onPick(q)}
                        className="group rounded-3xl border border-slate-900/10 bg-white p-5 text-left shadow-sm transition hover:border-indigo-300 hover:shadow-md dark:border-white/10 dark:bg-slate-900/55 dark:hover:border-indigo-500/40"
                    >
                        <div className="text-sm font-bold text-slate-900 group-hover:text-indigo-700 dark:text-slate-100 dark:group-hover:text-indigo-300 transition">
                            {q.title}
                        </div>
                        {q.description && (
                            <p className="mt-1 line-clamp-2 text-xs text-slate-500 dark:text-slate-400">{q.description}</p>
                        )}
                        <div className="mt-3 flex flex-wrap gap-1.5">
                            <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-semibold text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300">
                                {q.questions?.length ?? 0} întrebări
                            </span>
                            <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600 dark:bg-slate-800 dark:text-slate-400">
                                {Math.round(q.timeLimitSeconds / 60)} min
                            </span>
                            {(q.tags ?? []).slice(0, 2).map(t => (
                                <span key={t} className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">#{t}</span>
                            ))}
                        </div>
                        <div className="mt-4 flex items-center justify-end">
                            <span className="rounded-xl bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white group-hover:bg-indigo-700 transition dark:bg-indigo-500">
                                Lansează →
                            </span>
                        </div>
                    </button>
                ))}
            </div>

            {!loading && filtered.length === 0 && (
                <div className="rounded-3xl border border-slate-900/10 bg-white p-10 text-center text-sm text-slate-500 dark:border-white/10 dark:bg-slate-900/55">
                    Niciun quiz publicat găsit.
                </div>
            )}
        </div>
    );
}

// ── Main admin page ───────────────────────────────────────────────────────────
export function AdminLivePage() {
    const api = useApi();
    const lsApi = useMemo(() => liveSessionApi(api), [api]);
    const nav = useNavigate();
    const { code: codeParam } = useParams<{ code?: string }>();

    const [sessionCode, setSessionCode] = useState(codeParam ?? "");
    const [displayName] = useState("Host");
    const [phase, setPhase] = useState<"pick" | "lobby" | "session">(codeParam ? "lobby" : "pick");
    const [creating, setCreating] = useState(false);
    const [createErr, setCreateErr] = useState<string | null>(null);
    const [showEndConfirm, setShowEndConfirm] = useState(false);

    const { state, startSession, nextQuestion, endSession } = useLiveSession(
        sessionCode,
        displayName,
        phase === "lobby" || phase === "session",
    );

    // Sync phase with session status from hub
    useEffect(() => {
        if (state.status === "running" && phase !== "session") setPhase("session");
        if (state.status === "ended") setPhase("session");
    }, [state.status, phase]);

    const handlePick = async (quiz: Quiz) => {
        setCreating(true);
        setCreateErr(null);
        try {
            const res = await lsApi.create({ quizId: quiz.id });
            setSessionCode(res.sessionCode);
            setPhase("lobby");
            nav(`/app/live/host/${res.sessionCode}`, { replace: true });
        } catch (e: any) {
            setCreateErr(e?.response?.data?.message ?? "Nu pot crea sesiunea.");
        } finally {
            setCreating(false);
        }
    };

    const handleEnd = async () => {
        setShowEndConfirm(false);
        await endSession();
    };

    // ── Pick quiz ─────────────────────────────────────────────────────────────
    if (phase === "pick") {
        return (
            <div className="space-y-4">
                {createErr && (
                    <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                        {createErr}
                    </div>
                )}
                {creating ? (
                    <div className="flex items-center justify-center py-20">
                        <div className="flex flex-col items-center gap-3">
                            <div className="h-10 w-10 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent dark:border-indigo-400" />
                            <p className="text-sm text-slate-500 dark:text-slate-400">Se creează sesiunea...</p>
                        </div>
                    </div>
                ) : (
                    <PickQuizStep onPick={handlePick} />
                )}
            </div>
        );
    }

    // ── Session ended ─────────────────────────────────────────────────────────
    if (state.status === "ended") {
        return (
            <div className="space-y-6">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="flex items-center gap-3">
                        <span className="text-3xl">🏁</span>
                        <div>
                            <div className="text-2xl font-extrabold text-slate-900 dark:text-white">Sesiune terminată</div>
                            <div className="text-sm text-slate-500 dark:text-slate-400">Cod: <span className="font-mono font-bold">{sessionCode}</span></div>
                        </div>
                    </div>
                </div>

                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">🏆 Clasament final</div>
                    <LeaderboardPanel entries={state.leaderboard} />
                </div>

                <button onClick={() => { setPhase("pick"); setSessionCode(""); nav("/app/live/host"); }}
                        className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500">
                    Lansează altă sesiune
                </button>
            </div>
        );
    }

    // ── Lobby ─────────────────────────────────────────────────────────────────
    if (phase === "lobby" || state.status === "lobby") {
        return (
            <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
                <div className="space-y-5">
                    {/* Status bar */}
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex items-center justify-between">
                            <div>
                                <div className="flex items-center gap-2">
                                    <span className="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-amber-500" />
                                    <span className="text-lg font-bold text-slate-900 dark:text-white">Lobby — așteptare jucători</span>
                                </div>
                                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                                    {state.players.length} {state.players.length === 1 ? "jucător conectat" : "jucători conectați"}
                                </p>
                            </div>
                            <button
                                onClick={startSession}
                                disabled={state.players.length === 0 || state.status === "connecting"}
                                className="rounded-2xl bg-emerald-600 px-6 py-3 text-sm font-bold text-white hover:bg-emerald-700 disabled:opacity-50 dark:bg-emerald-500 dark:hover:bg-emerald-400 transition"
                            >
                                ▶ Start Quiz
                            </button>
                        </div>
                    </div>

                    {/* Code display */}
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <SessionCodeDisplay code={sessionCode} />
                    </div>

                    {/* Players grid */}
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">
                            Jucători în lobby
                        </div>
                        {state.players.length === 0 ? (
                            <div className="flex flex-col items-center py-8 text-center">
                                <div className="text-4xl">🎯</div>
                                <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                                    Distribuie codul studenților.<br />
                                    Ei se conectează la <span className="font-semibold">/app/live</span>
                                </p>
                            </div>
                        ) : (
                            <div className="flex flex-wrap gap-2">
                                {state.players.map(p => (
                                    <span key={p.id}
                                          className="flex items-center gap-1.5 rounded-full border border-indigo-100 bg-indigo-50 px-3 py-1.5 text-sm font-medium text-indigo-700 dark:border-indigo-500/20 dark:bg-indigo-500/10 dark:text-indigo-300">
                                        <span className="h-2 w-2 rounded-full bg-emerald-500" />
                                        {p.displayName}
                                    </span>
                                ))}
                            </div>
                        )}
                    </div>
                </div>

                {/* Sidebar */}
                <aside>
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Clasament live</div>
                        <LeaderboardPanel entries={state.leaderboard} compact />
                    </div>
                </aside>
            </div>
        );
    }

    // ── Running ───────────────────────────────────────────────────────────────
    const isLast = state.questionIndex >= state.totalQuestions - 1;

    return (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_300px]">
            {/* Main */}
            <div className="space-y-5">
                {/* Control bar */}
                <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                        <div className="flex items-center gap-3">
                            <span className="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-red-500" />
                            <div>
                                <span className="text-sm font-bold text-slate-900 dark:text-white">Live</span>
                                <span className="ml-2 text-sm text-slate-500 dark:text-slate-400">
                                    {state.players.length} jucători • cod <span className="font-mono font-bold">{sessionCode}</span>
                                </span>
                            </div>
                        </div>
                        <div className="flex gap-2">
                            <button
                                onClick={nextQuestion}
                                className="rounded-2xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white hover:bg-indigo-700 dark:bg-indigo-500 transition"
                            >
                                {isLast ? "Finalizează ✓" : `Întrebarea ${state.questionIndex + 2} →`}
                            </button>
                            <button
                                onClick={() => setShowEndConfirm(true)}
                                className="rounded-2xl border border-red-200 bg-red-50 px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800/50 dark:bg-red-900/20 dark:text-red-300 transition"
                            >
                                Stop
                            </button>
                        </div>
                    </div>

                    {/* Progress bar */}
                    <div className="mt-3">
                        <div className="flex justify-between text-xs text-slate-500 dark:text-slate-400 mb-1">
                            <span>Progres sesiune</span>
                            <span>{state.questionIndex + 1} / {state.totalQuestions}</span>
                        </div>
                        <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
                            <div className="h-1.5 rounded-full bg-indigo-500 transition-all duration-500"
                                 style={{ width: `${((state.questionIndex + 1) / Math.max(1, state.totalQuestions)) * 100}%` }} />
                        </div>
                    </div>
                </div>

                {/* Question + timer */}
                {state.currentQuestion && (
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex items-start justify-between gap-4">
                            <div className="flex-1 min-w-0">
                                <QuestionDisplay
                                    q={state.currentQuestion}
                                    index={state.questionIndex}
                                    total={state.totalQuestions}
                                />
                            </div>
                            {state.deadlineUtc && (
                                <div className="shrink-0">
                                    <Countdown deadline={state.deadlineUtc} total={state.timeLimitSeconds} />
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* Live responses counter */}
                <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">Jucători conectați</div>
                    <div className="flex flex-wrap gap-2">
                        {state.players.map(p => (
                            <span key={p.id}
                                  className="flex items-center gap-1.5 rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                                <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                                {p.displayName}
                            </span>
                        ))}
                    </div>
                </div>
            </div>

            {/* Sidebar */}
            <aside className="space-y-4">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">🏆 Clasament live</div>
                    <LeaderboardPanel entries={state.leaderboard} />
                </div>
            </aside>

            {/* End confirm modal */}
            {showEndConfirm && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm">
                    <div className="w-full max-w-sm rounded-3xl border border-slate-900/10 bg-white p-6 shadow-xl dark:border-white/10 dark:bg-slate-900">
                        <div className="text-lg font-bold text-slate-900 dark:text-white">Oprești sesiunea?</div>
                        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                            Se va afișa clasamentul final tuturor jucătorilor.
                        </p>
                        <div className="mt-5 flex gap-3">
                            <button onClick={() => setShowEndConfirm(false)}
                                    className="flex-1 rounded-2xl border border-slate-200 bg-white py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200">
                                Anulează
                            </button>
                            <button onClick={handleEnd}
                                    className="flex-1 rounded-2xl bg-red-600 py-2.5 text-sm font-bold text-white hover:bg-red-700">
                                Da, oprește
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}