import { useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import {
    useLiveSession,
    type AnswerPayload,
    type LeaderboardEntry,
    type QuizOption,
    type QuizQuestion,
} from "../../hooks/useLiveSession";

function cn(...xs: (string | false | null | undefined)[]) {
    return xs.filter(Boolean).join(" ");
}

// ── Countdown ring ────────────────────────────────────────────────────────────
function CountdownRing({ deadline, total }: { deadline: Date; total: number }) {
    const [left, setLeft] = useState(0);
    useEffect(() => {
        const t = setInterval(() => {
            setLeft(Math.max(0, Math.ceil((deadline.getTime() - Date.now()) / 1000)));
        }, 200);
        return () => clearInterval(t);
    }, [deadline]);

    const pct = total > 0 ? (left / total) * 100 : 0;
    const r = 36;
    const circ = 2 * Math.PI * r;

    return (
        <div className="relative inline-flex h-24 w-24 shrink-0 items-center justify-center">
            <svg className="absolute inset-0 -rotate-90" width="96" height="96" viewBox="0 0 96 96">
                <circle cx="48" cy="48" r={r} fill="none" strokeWidth="6"
                        className="stroke-slate-200 dark:stroke-slate-700" />
                <circle cx="48" cy="48" r={r} fill="none" strokeWidth="6"
                        strokeLinecap="round"
                        strokeDasharray={circ}
                        strokeDashoffset={circ * (1 - pct / 100)}
                        className={cn(
                            "transition-all duration-500",
                            pct < 25 ? "stroke-red-500" : pct < 50 ? "stroke-amber-400" : "stroke-emerald-500"
                        )} />
            </svg>
            <div className="text-center">
                <span className={cn(
                    "block text-2xl font-black tabular-nums",
                    left <= 5 ? "text-red-600 dark:text-red-400 animate-pulse" : "text-slate-800 dark:text-slate-100"
                )}>{left}</span>
                <span className="text-[9px] font-semibold uppercase tracking-wide text-slate-400">sec</span>
            </div>
        </div>
    );
}

// ── Leaderboard overlay card ──────────────────────────────────────────────────
function MiniLeaderboard({ entries, myId }: { entries: LeaderboardEntry[]; myId: string }) {
    const medals = ["🥇", "🥈", "🥉"];
    const myPos = entries.findIndex(e => e.playerId === myId);

    return (
        <div className="space-y-1.5">
            {entries.slice(0, 5).map((e, i) => (
                <div key={e.playerId} className={cn(
                    "flex items-center justify-between rounded-xl px-3 py-2 text-sm font-medium",
                    e.playerId === myId
                        ? "bg-indigo-600 text-white"
                        : i === 0 ? "bg-amber-50 text-amber-900 dark:bg-amber-500/10 dark:text-amber-200"
                            : "bg-slate-50 text-slate-700 dark:bg-slate-950/30 dark:text-slate-300"
                )}>
                    <span className="flex items-center gap-2 min-w-0">
                        <span className="w-6 shrink-0 text-center text-base">{medals[i] ?? `${i + 1}.`}</span>
                        <span className="truncate">{e.displayName}</span>
                        {e.playerId === myId && <span className="text-xs opacity-70">(tu)</span>}
                    </span>
                    <span className="ml-2 shrink-0 font-bold">{e.score}p</span>
                </div>
            ))}
            {myPos >= 5 && (
                <div className="flex items-center justify-between rounded-xl bg-indigo-600 px-3 py-2 text-sm font-medium text-white">
                    <span className="flex items-center gap-2">
                        <span className="w-6 text-center">{myPos + 1}.</span>
                        <span className="truncate">{entries[myPos]?.displayName}</span>
                        <span className="text-xs opacity-70">(tu)</span>
                    </span>
                    <span className="font-bold">{entries[myPos]?.score}p</span>
                </div>
            )}
        </div>
    );
}

// ── Answer choice button ──────────────────────────────────────────────────────
function ChoiceBtn({
                       label, letter, selected, disabled, correct, wrong, onClick,
                   }: {
    label: string; letter: string; selected: boolean; disabled: boolean;
    correct?: boolean; wrong?: boolean; onClick: () => void;
}) {
    return (
        <button
            onClick={onClick}
            disabled={disabled}
            className={cn(
                "group relative flex w-full items-center gap-3 overflow-hidden rounded-2xl border px-4 py-3.5 text-left text-sm font-medium transition-all duration-200",
                correct && "border-emerald-400 bg-emerald-50 text-emerald-800 dark:border-emerald-500/40 dark:bg-emerald-500/10 dark:text-emerald-200",
                wrong && "border-red-300 bg-red-50 text-red-700 opacity-70 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300",
                !correct && !wrong && selected && "border-indigo-400 bg-indigo-600 text-white shadow-lg shadow-indigo-500/20",
                !correct && !wrong && !selected && !disabled && "border-slate-200 bg-white text-slate-700 hover:border-indigo-300 hover:bg-indigo-50 dark:border-white/10 dark:bg-slate-950/20 dark:text-slate-200 dark:hover:border-indigo-500/30",
                !correct && !wrong && !selected && disabled && "border-slate-200 bg-white text-slate-500 cursor-not-allowed dark:border-white/10 dark:bg-slate-950/20 dark:text-slate-500",
            )}
        >
            <span className={cn(
                "flex h-8 w-8 shrink-0 items-center justify-center rounded-xl text-xs font-black",
                selected && !correct && !wrong ? "bg-white/20 text-white" : "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400",
                correct && "bg-emerald-500 text-white",
                wrong && "bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400"
            )}>
                {letter}
            </span>
            <span className="flex-1">{label}</span>
            {correct && <span className="text-emerald-500 text-lg">✓</span>}
            {wrong && selected && <span className="text-red-400 text-lg">✗</span>}
        </button>
    );
}

// ── Question player ───────────────────────────────────────────────────────────
function QuestionPlayer({
                            question, index, total, deadline, timeLimit, onSubmit, ack,
                        }: {
    question: QuizQuestion; index: number; total: number;
    deadline: Date; timeLimit: number;
    onSubmit: (p: AnswerPayload) => void;
    ack: { isCorrect: boolean; pointsEarned: number; yourScore: number; expired?: boolean } | null;
}) {
    const [boolAns, setBoolAns] = useState<boolean | null>(null);
    const [singleId, setSingleId] = useState<string | null>(null);
    const [multiIds, setMultiIds] = useState<Set<string>>(new Set());
    const [textAns, setTextAns] = useState("");
    const [submitted, setSubmitted] = useState(false);
    const inputRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        setBoolAns(null); setSingleId(null); setMultiIds(new Set()); setTextAns(""); setSubmitted(false);
    }, [question.id]);

    useEffect(() => {
        if (question.type === 3) inputRef.current?.focus();
    }, [question.id, question.type]);

    const handleSubmit = () => {
        if (submitted) return;
        setSubmitted(true);
        onSubmit({
            boolAnswer: question.type === 0 ? boolAns : null,
            singleOptionId: question.type === 1 ? singleId : null,
            multipleOptionIds: question.type === 2 ? Array.from(multiIds) : null,
            textAnswer: question.type === 3 ? textAns.trim() : null,
        });
    };

    const toggleMulti = (id: string) => {
        setMultiIds(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n; });
    };

    const hasAnswer = question.type === 0 ? boolAns !== null
        : question.type === 1 ? singleId !== null
            : question.type === 2 ? multiIds.size > 0
                : textAns.trim().length > 0;

    return (
        <div className="flex h-full flex-col gap-5">
            {/* Header */}
            <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 text-xs font-semibold text-slate-400 dark:text-slate-500">
                        <span>Întrebarea {index + 1} din {total}</span>
                        <span>•</span>
                        <span>{question.points} {question.points === 1 ? "punct" : "puncte"}</span>
                    </div>
                </div>
                <CountdownRing deadline={deadline} total={timeLimit} />
            </div>

            {/* Prompt */}
            <div className="rounded-2xl border border-slate-200 bg-slate-50 px-5 py-4 text-base font-semibold leading-relaxed text-slate-900 dark:border-white/10 dark:bg-slate-900/50 dark:text-slate-100">
                {question.prompt}
            </div>

            {/* Inputs */}
            <div className="flex-1 space-y-2.5">
                {question.type === 0 && (
                    <div className="grid grid-cols-2 gap-3">
                        {([true, false] as const).map(val => (
                            <ChoiceBtn
                                key={String(val)}
                                letter={val ? "A" : "B"}
                                label={val ? "Adevărat" : "Fals"}
                                selected={boolAns === val}
                                disabled={submitted}
                                correct={ack ? (ack.isCorrect && boolAns === val) : false}
                                wrong={ack ? (!ack.isCorrect && boolAns === val) : false}
                                onClick={() => !submitted && setBoolAns(val)}
                            />
                        ))}
                    </div>
                )}

                {(question.type === 1 || question.type === 2) && (
                    <div className="space-y-2.5">
                        {question.type === 2 && !submitted && (
                            <p className="text-xs text-slate-400 dark:text-slate-500">
                                Selectează toate răspunsurile corecte
                            </p>
                        )}
                        {question.options.map((opt, oi) => {
                            const sel = question.type === 1 ? singleId === opt.id : multiIds.has(opt.id);
                            return (
                                <ChoiceBtn
                                    key={opt.id}
                                    letter={String.fromCharCode(65 + oi)}
                                    label={opt.text}
                                    selected={sel}
                                    disabled={submitted}
                                    correct={ack?.isCorrect && sel}
                                    wrong={ack && !ack.isCorrect && sel}
                                    onClick={() => {
                                        if (submitted) return;
                                        question.type === 1 ? setSingleId(opt.id) : toggleMulti(opt.id);
                                    }}
                                />
                            );
                        })}
                    </div>
                )}

                {question.type === 3 && (
                    <div className="space-y-2">
                        <input
                            ref={inputRef}
                            disabled={submitted}
                            value={textAns}
                            onChange={e => setTextAns(e.target.value)}
                            onKeyDown={e => e.key === "Enter" && !submitted && hasAnswer && handleSubmit()}
                            placeholder="Scrie răspunsul tău..."
                            className={cn(
                                "w-full rounded-2xl border px-4 py-3.5 text-sm font-medium outline-none transition",
                                "border-slate-200 bg-white text-slate-900 focus:border-indigo-400 focus:ring-2 focus:ring-indigo-500/20",
                                "dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100",
                                submitted && "opacity-60 cursor-not-allowed"
                            )}
                        />
                    </div>
                )}
            </div>

            {/* Submit */}
            {!submitted && (
                <button
                    onClick={handleSubmit}
                    disabled={!hasAnswer}
                    className="w-full rounded-2xl bg-indigo-600 py-3.5 text-sm font-bold text-white transition hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed dark:bg-indigo-500 dark:hover:bg-indigo-400"
                >
                    Trimite răspuns →
                </button>
            )}

            {/* Ack banner */}
            {ack && (
                <div className={cn(
                    "flex items-center justify-between rounded-2xl border px-4 py-3 text-sm font-semibold",
                    ack.expired ? "border-slate-300 bg-slate-50 text-slate-600 dark:border-white/10 dark:bg-slate-900/30 dark:text-slate-400"
                        : ack.isCorrect
                            ? "border-emerald-300 bg-emerald-50 text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300"
                            : "border-red-300 bg-red-50 text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300"
                )}>
                    <span>
                        {ack.expired ? "⏰ Timp expirat" : ack.isCorrect ? `✓ Corect! +${ack.pointsEarned}p` : "✗ Greșit"}
                    </span>
                    <span className="font-normal text-slate-500 dark:text-slate-400">
                        Scor total: <strong className="text-slate-800 dark:text-slate-200">{ack.yourScore}p</strong>
                    </span>
                </div>
            )}

            {submitted && !ack && (
                <div className="flex items-center gap-2 text-sm text-slate-400 dark:text-slate-500">
                    <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-500" />
                    Răspuns trimis — se evaluează...
                </div>
            )}
        </div>
    );
}

// ── Join form ─────────────────────────────────────────────────────────────────
function JoinForm({ defaultCode, onJoin }: { defaultCode: string; onJoin: (code: string, name: string) => void }) {
    const { user } = useAuth();
    const [code, setCode] = useState(defaultCode);
    const [name, setName] = useState(user?.email?.split("@")[0] ?? "");
    const [err, setErr] = useState("");

    const handleSubmit = () => {
        if (code.trim().length < 4) { setErr("Codul trebuie să aibă cel puțin 4 caractere."); return; }
        if (!name.trim()) { setErr("Introdu un nume de afișare."); return; }
        onJoin(code.trim().toUpperCase(), name.trim());
    };

    return (
        <div className="mx-auto max-w-sm space-y-6">
            {/* Hero */}
            <div className="text-center">
                <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-3xl bg-indigo-600 text-3xl shadow-lg shadow-indigo-500/30 dark:bg-indigo-500">
                    🎮
                </div>
                <h1 className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Intră în quiz live
                </h1>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                    Introdu codul primit de la profesor
                </p>
            </div>

            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="space-y-4">
                    <div>
                        <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Cod sesiune
                        </label>
                        <input
                            value={code}
                            onChange={e => setCode(e.target.value.toUpperCase())}
                            onKeyDown={e => e.key === "Enter" && handleSubmit()}
                            placeholder="ex: AB3X7K"
                            maxLength={6}
                            className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3.5 text-center font-mono text-xl font-black tracking-widest text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-500/20 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        />
                    </div>

                    <div>
                        <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Numele tău
                        </label>
                        <input
                            value={name}
                            onChange={e => setName(e.target.value)}
                            onKeyDown={e => e.key === "Enter" && handleSubmit()}
                            placeholder="Cum vrei să apari în clasament"
                            className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3.5 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-500/20 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        />
                    </div>

                    {err && (
                        <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                            {err}
                        </div>
                    )}

                    <button
                        onClick={handleSubmit}
                        className="w-full rounded-2xl bg-indigo-600 py-3.5 text-sm font-bold text-white shadow-sm shadow-indigo-500/20 hover:bg-indigo-700 transition dark:bg-indigo-500 dark:hover:bg-indigo-400"
                    >
                        Intră în sesiune →
                    </button>
                </div>
            </div>
        </div>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────
export function JoinLivePage() {
    const { code: codeParam } = useParams<{ code?: string }>();
    const nav = useNavigate();
    const { user } = useAuth();

    const [sessionCode, setSessionCode] = useState(codeParam?.toUpperCase() ?? "");
    const [displayName, setDisplayName] = useState(user?.email?.split("@")[0] ?? "");
    const [joined, setJoined] = useState(false);
    const [myConnectionId] = useState(() => Math.random().toString(36).slice(2));

    const { state, submitAnswer } = useLiveSession(
        sessionCode,
        displayName,
        joined,
    );

    const handleJoin = (code: string, name: string) => {
        setSessionCode(code);
        setDisplayName(name);
        setJoined(true);
        nav(`/app/live/join/${code}`, { replace: true });
    };

    // ── Not joined ────────────────────────────────────────────────────────────
    if (!joined) {
        return (
            <div className="flex min-h-[60vh] items-center justify-center px-4">
                <JoinForm defaultCode={codeParam ?? ""} onJoin={handleJoin} />
            </div>
        );
    }

    // ── Connecting ────────────────────────────────────────────────────────────
    if (state.status === "connecting" || state.status === "idle") {
        return (
            <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4">
                <div className="h-10 w-10 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent dark:border-indigo-400" />
                <p className="text-sm text-slate-500 dark:text-slate-400">
                    Se conectează la sesiunea <span className="font-mono font-bold text-slate-800 dark:text-slate-200">{sessionCode}</span>...
                </p>
            </div>
        );
    }

    // ── Error ─────────────────────────────────────────────────────────────────
    if (state.status === "error") {
        return (
            <div className="mx-auto max-w-sm space-y-4 pt-10 text-center">
                <div className="text-5xl">⚠️</div>
                <div className="text-lg font-bold text-slate-900 dark:text-white">Conexiune pierdută</div>
                <p className="text-sm text-slate-500 dark:text-slate-400">{state.error}</p>
                <button onClick={() => { setJoined(false); nav("/app/live"); }}
                        className="rounded-2xl bg-indigo-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500">
                    Încearcă din nou
                </button>
            </div>
        );
    }

    // ── Session ended ─────────────────────────────────────────────────────────
    if (state.status === "ended") {
        const myEntry = state.leaderboard.find(e => e.displayName === displayName);
        const myRank = state.leaderboard.findIndex(e => e.displayName === displayName) + 1;

        return (
            <div className="mx-auto max-w-lg space-y-6 py-6">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 text-center shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-6xl mb-3">
                        {myRank === 1 ? "🏆" : myRank === 2 ? "🥈" : myRank === 3 ? "🥉" : "🎉"}
                    </div>
                    <div className="text-2xl font-extrabold text-slate-900 dark:text-white">
                        {myRank === 1 ? "Felicitări, ai câștigat!" : `Locul ${myRank}`}
                    </div>
                    {myEntry && (
                        <div className="mt-2 text-3xl font-black text-indigo-600 dark:text-indigo-400">
                            {myEntry.score} puncte
                        </div>
                    )}
                </div>

                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Clasament final</div>
                    <MiniLeaderboard entries={state.leaderboard} myId={myConnectionId} />
                </div>

                <button onClick={() => nav("/app")}
                        className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-bold text-white hover:bg-indigo-700 dark:bg-indigo-500">
                    Înapoi la Dashboard
                </button>
            </div>
        );
    }

    // ── Lobby ─────────────────────────────────────────────────────────────────
    if (state.status === "lobby") {
        return (
            <div className="mx-auto max-w-sm py-8 text-center space-y-6">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-8 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-5xl mb-4">🎯</div>
                    <div className="text-xl font-extrabold text-slate-900 dark:text-white">
                        Ești în lobby!
                    </div>
                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        Bun venit, <strong className="text-slate-800 dark:text-slate-200">{displayName}</strong>.<br />
                        Aștepți ca profesorul să înceapă sesiunea.
                    </p>

                    <div className="mt-6 inline-flex items-center gap-2 rounded-2xl bg-indigo-50 px-4 py-2 dark:bg-indigo-500/10">
                        <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-indigo-500" />
                        <span className="text-sm font-semibold text-indigo-700 dark:text-indigo-300">
                            {state.players.length} {state.players.length === 1 ? "jucător" : "jucători"} conectați
                        </span>
                    </div>

                    {state.players.length > 1 && (
                        <div className="mt-4 flex flex-wrap justify-center gap-1.5">
                            {state.players.map(p => (
                                <span key={p.id} className={cn(
                                    "rounded-full px-2.5 py-1 text-xs font-medium",
                                    p.displayName === displayName
                                        ? "bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300"
                                        : "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400"
                                )}>
                                    {p.displayName}
                                </span>
                            ))}
                        </div>
                    )}
                </div>

                <div className="text-xs text-slate-400 dark:text-slate-500 animate-pulse">
                    Sesiunea va începe în curând...
                </div>
            </div>
        );
    }

    // ── Running ───────────────────────────────────────────────────────────────
    return (
        <div className="mx-auto max-w-2xl">
            <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_220px]">
                {/* Question */}
                <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    {state.currentQuestion && state.deadlineUtc ? (
                        <QuestionPlayer
                            question={state.currentQuestion}
                            index={state.questionIndex}
                            total={state.totalQuestions}
                            deadline={state.deadlineUtc}
                            timeLimit={state.timeLimitSeconds}
                            onSubmit={submitAnswer}
                            ack={state.lastAck}
                        />
                    ) : (
                        <div className="flex items-center gap-3 py-6 text-sm text-slate-400 dark:text-slate-500">
                            <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-500" />
                            Se pregătește întrebarea...
                        </div>
                    )}
                </div>

                {/* Sidebar */}
                <aside className="space-y-4">
                    {/* Progress */}
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-2 flex justify-between text-xs text-slate-500 dark:text-slate-400">
                            <span>Progres</span>
                            <span>{state.questionIndex + 1}/{state.totalQuestions}</span>
                        </div>
                        <div className="h-1.5 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
                            <div className="h-1.5 rounded-full bg-indigo-500 transition-all duration-500"
                                 style={{ width: `${((state.questionIndex + 1) / Math.max(1, state.totalQuestions)) * 100}%` }} />
                        </div>
                        {state.lastAck && (
                            <div className="mt-2 text-xs font-semibold text-slate-500 dark:text-slate-400">
                                Scor: <span className="text-indigo-600 dark:text-indigo-400">{state.lastAck.yourScore}p</span>
                            </div>
                        )}
                    </div>

                    {/* Mini leaderboard */}
                    {state.leaderboard.length > 0 && (
                        <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                            <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">
                                Clasament
                            </div>
                            <MiniLeaderboard entries={state.leaderboard} myId={myConnectionId} />
                        </div>
                    )}
                </aside>
            </div>
        </div>
    );
}