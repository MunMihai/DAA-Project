import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useLiveSession, type AnswerPayload, type LeaderboardEntry, type QuizQuestion } from "../../hooks/useLiveSession";
import { liveSessionApi } from "../../api/liveSessionApi";
import { quizApi } from "../../api/quizApi";
import { useApi } from "../../api/axios";
import { useAuth } from "../../auth/AuthContext";

// ── Utility ───────────────────────────────────────────────────────────────────
function cn(...xs: (string | false | null | undefined)[]) {
    return xs.filter(Boolean).join(" ");
}

// ── Countdown timer ───────────────────────────────────────────────────────────
function Countdown({ deadlineUtc, totalSeconds }: { deadlineUtc: Date; totalSeconds: number }) {
    const [remaining, setRemaining] = useState(0);

    useEffect(() => {
        const tick = () => {
            const left = Math.max(0, Math.ceil((deadlineUtc.getTime() - Date.now()) / 1000));
            setRemaining(left);
        };
        tick();
        const id = setInterval(tick, 250);
        return () => clearInterval(id);
    }, [deadlineUtc]);

    const pct = totalSeconds > 0 ? (remaining / totalSeconds) * 100 : 0;
    const color = pct > 50 ? "bg-emerald-500" : pct > 25 ? "bg-amber-500" : "bg-red-500";

    return (
        <div className="space-y-1">
            <div className="flex justify-between text-xs font-semibold text-slate-600 dark:text-slate-400">
                <span>Timp rămas</span>
                <span className={remaining <= 5 ? "text-red-600 dark:text-red-400 animate-pulse" : ""}>{remaining}s</span>
            </div>
            <div className="h-2 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
                <div
                    className={`h-2 rounded-full transition-all duration-500 ${color}`}
                    style={{ width: `${pct}%` }}
                />
            </div>
        </div>
    );
}

// ── Leaderboard ───────────────────────────────────────────────────────────────
function Leaderboard({ entries }: { entries: LeaderboardEntry[] }) {
    if (entries.length === 0) return (
        <div className="text-sm text-slate-500 dark:text-slate-400">Niciun jucător încă.</div>
    );

    const medals = ["🥇", "🥈", "🥉"];

    return (
        <ol className="space-y-2">
            {entries.map((e, i) => (
                <li
                    key={e.playerId}
                    className={cn(
                        "flex items-center justify-between rounded-2xl px-4 py-2 text-sm font-medium",
                        i === 0 && "bg-amber-50 text-amber-900 dark:bg-amber-500/10 dark:text-amber-200",
                        i === 1 && "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
                        i === 2 && "bg-orange-50 text-orange-900 dark:bg-orange-500/10 dark:text-orange-200",
                        i > 2 && "bg-slate-50 text-slate-700 dark:bg-slate-900/30 dark:text-slate-300"
                    )}
                >
                    <span className="flex items-center gap-2">
                        <span className="w-5 text-base">{medals[i] ?? `${i + 1}.`}</span>
                        <span className="truncate max-w-[140px]">{e.displayName}</span>
                    </span>
                    <span className="font-bold">{e.score} pt</span>
                </li>
            ))}
        </ol>
    );
}

// ── Question player view ──────────────────────────────────────────────────────
function QuestionPlayer({
                            question,
                            questionIndex,
                            totalQuestions,
                            deadlineUtc,
                            timeLimitSeconds,
                            onSubmit,
                            ack,
                        }: {
    question: QuizQuestion;
    questionIndex: number;
    totalQuestions: number;
    deadlineUtc: Date;
    timeLimitSeconds: number;
    onSubmit: (p: AnswerPayload) => void;
    ack: { isCorrect: boolean; pointsEarned: number; yourScore: number } | null;
}) {
    const [boolAns, setBoolAns] = useState<boolean | null>(null);
    const [singleId, setSingleId] = useState<string | null>(null);
    const [multiIds, setMultiIds] = useState<Set<string>>(new Set());
    const [textAns, setTextAns] = useState("");
    const [submitted, setSubmitted] = useState(false);

    // Reset on question change
    useEffect(() => {
        setBoolAns(null);
        setSingleId(null);
        setMultiIds(new Set());
        setTextAns("");
        setSubmitted(false);
    }, [question.id]);

    const handleSubmit = () => {
        if (submitted) return;

        const payload: AnswerPayload = {
            boolAnswer: question.type === 0 ? boolAns : null,
            singleOptionId: question.type === 1 ? singleId : null,
            multipleOptionIds: question.type === 2 ? Array.from(multiIds) : null,
            textAnswer: question.type === 3 ? textAns : null,
        };

        setSubmitted(true);
        onSubmit(payload);
    };

    const toggleMulti = (id: string) => {
        setMultiIds((prev) => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id); else next.add(id);
            return next;
        });
    };

    return (
        <div className="space-y-5">
            <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                <span>Întrebarea {questionIndex + 1} / {totalQuestions}</span>
                <span>{question.points} pt</span>
            </div>

            <Countdown deadlineUtc={deadlineUtc} totalSeconds={timeLimitSeconds} />

            <div className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 text-sm font-medium text-slate-900 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100">
                {question.prompt}
            </div>

            {/* Answer input */}
            <div className="space-y-2">
                {question.type === 0 && (
                    <div className="flex gap-3">
                        {[true, false].map((val) => (
                            <button
                                key={String(val)}
                                disabled={submitted}
                                onClick={() => setBoolAns(val)}
                                className={cn(
                                    "flex-1 rounded-2xl border py-3 text-sm font-semibold transition",
                                    boolAns === val
                                        ? "border-indigo-400 bg-indigo-600 text-white dark:border-indigo-500 dark:bg-indigo-500"
                                        : "border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200",
                                    submitted && "opacity-60 cursor-not-allowed"
                                )}
                            >
                                {val ? "True" : "False"}
                            </button>
                        ))}
                    </div>
                )}

                {(question.type === 1 || question.type === 2) && (
                    <div className="space-y-2">
                        {question.options.map((opt) => {
                            const sel = question.type === 1
                                ? singleId === opt.id
                                : multiIds.has(opt.id);
                            return (
                                <button
                                    key={opt.id}
                                    disabled={submitted}
                                    onClick={() =>
                                        question.type === 1
                                            ? setSingleId(opt.id)
                                            : toggleMulti(opt.id)
                                    }
                                    className={cn(
                                        "w-full rounded-2xl border px-4 py-3 text-left text-sm font-medium transition",
                                        sel
                                            ? "border-indigo-400 bg-indigo-50 text-indigo-800 dark:border-indigo-500/40 dark:bg-indigo-500/10 dark:text-indigo-200"
                                            : "border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200",
                                        submitted && "opacity-60 cursor-not-allowed"
                                    )}
                                >
                                    {opt.text}
                                </button>
                            );
                        })}
                    </div>
                )}

                {question.type === 3 && (
                    <input
                        disabled={submitted}
                        value={textAns}
                        onChange={(e) => setTextAns(e.target.value)}
                        onKeyDown={(e) => e.key === "Enter" && handleSubmit()}
                        placeholder="Răspunsul tău..."
                        className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 disabled:opacity-60"
                    />
                )}
            </div>

            {!submitted && (
                <button
                    onClick={handleSubmit}
                    className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                >
                    Trimite răspuns
                </button>
            )}

            {/* Ack */}
            {ack && (
                <div className={cn(
                    "rounded-2xl border px-4 py-3 text-sm font-semibold",
                    ack.isCorrect
                        ? "border-emerald-300 bg-emerald-50 text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300"
                        : "border-red-300 bg-red-50 text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300"
                )}>
                    {ack.isCorrect ? `✓ Corect! +${ack.pointsEarned} puncte` : "✗ Greșit"}
                    <span className="ml-3 font-normal text-slate-600 dark:text-slate-400">
                        Scorul tău: {ack.yourScore} pt
                    </span>
                </div>
            )}
        </div>
    );
}

// ── Create session modal ──────────────────────────────────────────────────────
function CreateSessionModal({
                                onCreated,
                                onClose,
                            }: {
    onCreated: (code: string) => void;
    onClose: () => void;
}) {
    const api = useApi();
    const qapi = useMemo(() => quizApi(api), [api]);
    const lsApi = useMemo(() => liveSessionApi(api), [api]);

    const [quizzes, setQuizzes] = useState<{ id: string; title: string }[]>([]);
    const [selectedQuizId, setSelectedQuizId] = useState("");
    const [loading, setLoading] = useState(false);
    const [err, setErr] = useState<string | null>(null);

    useEffect(() => {
        qapi.list({ status: 1 }).then((data) =>
            setQuizzes(data.map((q) => ({ id: q.id, title: q.title })))
        ).catch(() => setErr("Nu pot încărca quizurile."));
    }, [qapi]);

    const handleCreate = async () => {
        if (!selectedQuizId) { setErr("Selectează un quiz."); return; }
        setLoading(true);
        setErr(null);
        try {
            const res = await lsApi.create({ quizId: selectedQuizId });
            onCreated(res.sessionCode);
        } catch (e: any) {
            setErr(e?.response?.data?.message ?? "Eroare la creare.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm">
            <div className="w-full max-w-md rounded-3xl border border-slate-900/10 bg-white p-6 shadow-xl dark:border-white/10 dark:bg-slate-900">
                <div className="text-lg font-bold text-slate-900 dark:text-white">Creează sesiune live</div>
                <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">Selectează un quiz publicat.</div>

                <div className="mt-4 space-y-3">
                    <select
                        value={selectedQuizId}
                        onChange={(e) => setSelectedQuizId(e.target.value)}
                        className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                    >
                        <option value="">— Alege quiz —</option>
                        {quizzes.map((q) => (
                            <option key={q.id} value={q.id}>{q.title}</option>
                        ))}
                    </select>

                    {err && (
                        <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                            {err}
                        </div>
                    )}

                    <div className="flex gap-3">
                        <button
                            onClick={onClose}
                            className="flex-1 rounded-2xl border border-slate-200 bg-white py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200"
                        >
                            Anulează
                        </button>
                        <button
                            onClick={handleCreate}
                            disabled={loading}
                            className="flex-1 rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 dark:bg-indigo-500"
                        >
                            {loading ? "Se creează..." : "Creează"}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────
export function LiveSessionPage() {
    const { user } = useAuth();
    const { code: codeParam } = useParams<{ code?: string }>();
    const [searchParams] = useSearchParams();
    const nav = useNavigate();

    const isHostParam = searchParams.get("host") === "1";

    const [sessionCode, setSessionCode] = useState(codeParam?.toUpperCase() ?? "");
    const [joinCode, setJoinCode] = useState("");
    const [displayName, setDisplayName] = useState(user?.email?.split("@")[0] ?? "Player");
    const [joined, setJoined] = useState(!!codeParam);
    const [isHost, setIsHost] = useState(isHostParam);
    const [showCreate, setShowCreate] = useState(false);

    const { state, startSession, nextQuestion, submitAnswer, endSession } = useLiveSession(
        joined ? sessionCode : "",
        displayName,
        isHost
    );

    const handleJoin = () => {
        const code = joinCode.trim().toUpperCase();
        if (code.length < 4) return;
        setSessionCode(code);
        setIsHost(false);
        setJoined(true);
        nav(`/app/live/${code}`, { replace: true });
    };

    const handleCreated = (code: string) => {
        setSessionCode(code);
        setIsHost(true);
        setJoined(true);
        setShowCreate(false);
        nav(`/app/live/${code}?host=1`, { replace: true });
    };

    // ── Not yet joined ────────────────────────────────────────────────────────
    if (!joined) {
        return (
            <>
                {showCreate && (
                    <CreateSessionModal
                        onCreated={handleCreated}
                        onClose={() => setShowCreate(false)}
                    />
                )}

                <div className="mx-auto max-w-md space-y-6">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="text-2xl font-extrabold text-slate-900 dark:text-white">Live Quiz</div>
                        <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                            Intră într-o sesiune live sau creează una nouă.
                        </div>

                        <div className="mt-6 space-y-3">
                            <div>
                                <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                                    Nume afișat
                                </label>
                                <input
                                    value={displayName}
                                    onChange={(e) => setDisplayName(e.target.value)}
                                    className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                    placeholder="Numele tău..."
                                />
                            </div>

                            <div>
                                <label className="mb-1 block text-sm font-medium text-slate-800 dark:text-slate-200">
                                    Cod sesiune
                                </label>
                                <div className="flex gap-2">
                                    <input
                                        value={joinCode}
                                        onChange={(e) => setJoinCode(e.target.value.toUpperCase())}
                                        onKeyDown={(e) => e.key === "Enter" && handleJoin()}
                                        placeholder="ex: AB3X7K"
                                        maxLength={6}
                                        className="flex-1 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-mono text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                    />
                                    <button
                                        onClick={handleJoin}
                                        className="rounded-2xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                                    >
                                        Join
                                    </button>
                                </div>
                            </div>

                            <div className="relative flex items-center py-2">
                                <div className="flex-1 border-t border-slate-200 dark:border-white/10" />
                                <span className="mx-4 text-xs text-slate-500 dark:text-slate-400">sau</span>
                                <div className="flex-1 border-t border-slate-200 dark:border-white/10" />
                            </div>

                            <button
                                onClick={() => setShowCreate(true)}
                                className="w-full rounded-2xl border border-indigo-200 bg-indigo-50 py-3 text-sm font-semibold text-indigo-700 hover:bg-indigo-100 dark:border-indigo-500/30 dark:bg-indigo-500/10 dark:text-indigo-200"
                            >
                                + Creează sesiune nouă (host)
                            </button>
                        </div>
                    </div>
                </div>
            </>
        );
    }

    // ── Connection state ──────────────────────────────────────────────────────
    if (state.status === "connecting" || state.status === "idle") {
        return (
            <div className="grid min-h-[40vh] place-items-center">
                <div className="text-sm text-slate-600 dark:text-slate-400 animate-pulse">
                    Conectare la sesiunea {sessionCode}...
                </div>
            </div>
        );
    }

    if (state.status === "error") {
        return (
            <div className="mx-auto max-w-md rounded-3xl border border-red-200 bg-red-50 p-6 dark:border-red-800 dark:bg-red-900/30">
                <div className="text-sm font-semibold text-red-700 dark:text-red-200">Eroare conexiune</div>
                <div className="mt-1 text-sm text-red-600 dark:text-red-300">{state.error}</div>
                <button
                    onClick={() => nav("/app/live")}
                    className="mt-4 rounded-xl bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700"
                >
                    Înapoi
                </button>
            </div>
        );
    }

    // ── Session ended ─────────────────────────────────────────────────────────
    if (state.status === "ended") {
        return (
            <div className="mx-auto max-w-lg space-y-6">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-2xl font-extrabold text-slate-900 dark:text-white">
                        🏁 Sesiune terminată!
                    </div>
                    <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">Cod: {sessionCode}</div>
                </div>

                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Clasament final</div>
                    <Leaderboard entries={state.leaderboard} />
                </div>

                <button
                    onClick={() => nav("/app")}
                    className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                >
                    Înapoi la Dashboard
                </button>
            </div>
        );
    }

    // ── Lobby ─────────────────────────────────────────────────────────────────
    const isLobby = state.status === "lobby";
    const isRunning = state.status === "running";

    return (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_300px]">
            {/* Main area */}
            <div className="space-y-6">
                {/* Header */}
                <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <div>
                            <div className="flex items-center gap-3">
                                <div className="text-xl font-bold text-slate-900 dark:text-white">
                                    {isLobby ? "🎯 Lobby" : `❓ Întrebarea ${state.questionIndex + 1}`}
                                </div>
                                <span className={cn(
                                    "rounded-full px-2.5 py-1 text-xs font-semibold",
                                    isLobby
                                        ? "bg-amber-100 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300"
                                        : "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
                                )}>
                                    {isLobby ? "Așteptare" : "Live"}
                                </span>
                            </div>
                            <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                                Cod sesiune: <span className="font-mono font-bold text-slate-900 dark:text-white">{sessionCode}</span>
                                {isHost && <span className="ml-2 text-xs text-indigo-600 dark:text-indigo-300">(host)</span>}
                            </div>
                        </div>

                        {isHost && (
                            <div className="flex gap-2">
                                {isLobby && (
                                    <button
                                        onClick={startSession}
                                        disabled={state.players.length === 0}
                                        className="rounded-2xl bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50 dark:bg-emerald-500"
                                    >
                                        ▶ Start
                                    </button>
                                )}
                                {isRunning && (
                                    <>
                                        <button
                                            onClick={nextQuestion}
                                            className="rounded-2xl bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                                        >
                                            Întrebarea {state.questionIndex + 2 <= state.totalQuestions
                                            ? `${state.questionIndex + 2} →`
                                            : "Finalizează ✓"
                                        }
                                        </button>
                                        <button
                                            onClick={endSession}
                                            className="rounded-2xl border border-red-200 bg-red-50 px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-200"
                                        >
                                            Stop
                                        </button>
                                    </>
                                )}
                            </div>
                        )}
                    </div>
                </div>

                {/* Lobby players */}
                {isLobby && (
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-900 dark:text-slate-100">
                            Jucători în lobby ({state.players.length})
                        </div>
                        {state.players.length === 0 ? (
                            <div className="text-sm text-slate-500 dark:text-slate-400 animate-pulse">
                                Se așteaptă jucători... Distribuie codul <span className="font-mono font-bold">{sessionCode}</span>
                            </div>
                        ) : (
                            <div className="flex flex-wrap gap-2">
                                {state.players.map((p) => (
                                    <span
                                        key={p.id}
                                        className="rounded-full border border-indigo-200 bg-indigo-50 px-3 py-1 text-sm font-medium text-indigo-700 dark:border-indigo-500/30 dark:bg-indigo-500/10 dark:text-indigo-200"
                                    >
                                        {p.displayName}
                                    </span>
                                ))}
                            </div>
                        )}

                        {!isHost && (
                            <div className="mt-4 text-xs text-slate-500 dark:text-slate-400 animate-pulse">
                                Aștepți ca host-ul să înceapă sesiunea...
                            </div>
                        )}
                    </div>
                )}

                {/* Question area */}
                {isRunning && state.currentQuestion && (
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        {isHost ? (
                            /* Host sees question + live stats */
                            <div className="space-y-4">
                                <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                                    <span>Întrebarea {state.questionIndex + 1} / {state.totalQuestions}</span>
                                    <span>{state.currentQuestion.points} pt</span>
                                </div>

                                {state.deadlineUtc && (
                                    <Countdown
                                        deadlineUtc={state.deadlineUtc}
                                        totalSeconds={state.timeLimitSeconds}
                                    />
                                )}

                                <div className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 text-sm font-medium text-slate-900 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100">
                                    {state.currentQuestion.prompt}
                                </div>

                                {state.currentQuestion.options.length > 0 && (
                                    <div className="space-y-2">
                                        {state.currentQuestion.options.map((opt) => (
                                            <div
                                                key={opt.id}
                                                className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300"
                                            >
                                                {opt.text}
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        ) : (
                            /* Player interactive view */
                            <QuestionPlayer
                                question={state.currentQuestion}
                                questionIndex={state.questionIndex}
                                totalQuestions={state.totalQuestions}
                                deadlineUtc={state.deadlineUtc!}
                                timeLimitSeconds={state.timeLimitSeconds}
                                onSubmit={submitAnswer}
                                ack={state.lastAnswerAck}
                            />
                        )}
                    </div>
                )}
            </div>

            {/* Sidebar: leaderboard */}
            <aside className="space-y-4">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-3 text-sm font-semibold text-slate-900 dark:text-slate-100">
                        🏆 Clasament
                    </div>
                    <Leaderboard entries={state.leaderboard} />
                </div>

                {isRunning && (
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">Progres</div>
                        <div className="mt-2">
                            <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
                                <div
                                    className="h-1.5 rounded-full bg-indigo-500 transition-all"
                                    style={{
                                        width: state.totalQuestions > 0
                                            ? `${((state.questionIndex + 1) / state.totalQuestions) * 100}%`
                                            : "0%"
                                    }}
                                />
                            </div>
                            <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                                {state.questionIndex + 1} / {state.totalQuestions} întrebări
                            </div>
                        </div>
                    </div>
                )}
            </aside>
        </div>
    );
}