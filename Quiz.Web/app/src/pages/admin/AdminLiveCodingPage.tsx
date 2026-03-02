import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useApi } from "../../api/axios";
import { codingApi, type CodingRuleset } from "../../api/codingApi";
import { useLiveCodingSession, type LeaderboardEntry } from "../../hooks/useLiveCodingSession";

function cn(...xs: (string | false | null | undefined)[]) {
    return xs.filter(Boolean).join(" ");
}

function CountdownTimer({ deadlineUtc, onExpire }: { deadlineUtc: Date | null, onExpire?: () => void }) {
    const [timeLeft, setTimeLeft] = useState(0);

    useEffect(() => {
        if (!deadlineUtc) return;
        const calc = () => Math.max(0, Math.floor((deadlineUtc.getTime() - Date.now()) / 1000));
        setTimeLeft(calc());

        const timer = setInterval(() => {
            const left = calc();
            setTimeLeft(left);
            if (left <= 0) {
                clearInterval(timer);
                if (onExpire) onExpire();
            }
        }, 1000);

        return () => clearInterval(timer);
    }, [deadlineUtc, onExpire]);

    if (!deadlineUtc) return null;

    const m = Math.floor(timeLeft / 60).toString().padStart(2, '0');
    const s = (timeLeft % 60).toString().padStart(2, '0');

    return (
        <div className="font-mono text-xl font-bold text-red-600 dark:text-red-400">
            {m}:{s}
        </div>
    );
}

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

function SessionCodeDisplay({ code }: { code: string }) {
    const [copied, setCopied] = useState(false);
    const copy = () => {
        navigator.clipboard.writeText(code);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <div className="flex flex-col items-center gap-3">
            <div className="rounded-2xl bg-indigo-600 px-8 py-5 text-center dark:bg-indigo-500">
                <div className="text-xs font-semibold uppercase tracking-widest text-indigo-200">Cod sesiune</div>
                <div className="mt-1 font-mono text-5xl font-black tracking-widest text-white">{code}</div>
            </div>
            <button onClick={copy}
                className="rounded-xl border border-slate-200 bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200">
                {copied ? "✓ Copiat!" : "Copiază codul"}
            </button>
            <p className="text-center text-xs text-slate-500 dark:text-slate-400">
                Studenții accesează <span className="font-semibold text-slate-700 dark:text-slate-200">/app/coding-live</span> și introduc codul
            </p>
        </div>
    );
}

function CreateSessionStep({ onCreate }: { onCreate: (code: string) => void }) {
    const api = useApi();
    const coding = codingApi(api);
    const [referenceCode, setReferenceCode] = useState("");
    const [ruleset, setRuleset] = useState<CodingRuleset | null>(null);
    const [timeLimit, setTimeLimit] = useState(10);
    const [loading, setLoading] = useState(false);
    const [creating, setCreating] = useState(false);
    const [error, setError] = useState("");

    const handleGenerate = async () => {
        if (!referenceCode.trim()) return;
        setLoading(true);
        setError("");
        try {
            const result = await coding.generateRuleset(referenceCode);
            setRuleset(result);
        } catch (err: any) {
            setError(err.response?.data?.message || err.message || "Eroare la generare");
        } finally {
            setLoading(false);
        }
    };

    const handleCreate = async () => {
        if (!ruleset) return;
        setCreating(true);
        setError("");
        try {
            const res = await coding.createSession(ruleset, timeLimit * 60);
            onCreate(res.sessionCode);
        } catch (err: any) {
            setError(err.response?.data?.message || err.message || "Eroare la creare");
        } finally {
            setCreating(false);
        }
    };

    return (
        <div className="space-y-6">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Lansează Coding Sesiune
                </div>
                <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                    Inserează codul sursă de referință. Va fi generat un ruleset pentru autoevaluare.
                </p>

                <textarea
                    className="w-full h-64 mt-4 p-4 border rounded-xl font-mono text-sm bg-slate-50 text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 dark:bg-slate-950 dark:border-white/10 dark:text-slate-100"
                    value={referenceCode}
                    onChange={(e: any) => setReferenceCode(e.target.value)}
                    placeholder="public class Solution { ... }"
                />

                <div className="mt-4">
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-1">
                        Timp limită (minute)
                    </label>
                    <input type="number" min="1" max="120"
                        className="w-32 px-3 py-2 border rounded-xl bg-slate-50 dark:bg-slate-950 dark:border-white/10 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                        value={timeLimit} onChange={e => setTimeLimit(parseInt(e.target.value) || 10)} />
                </div>

                <div className="mt-4 flex gap-3">
                    <button
                        className="px-6 py-2.5 bg-indigo-600 text-white font-semibold rounded-xl hover:bg-indigo-700 disabled:opacity-50 dark:bg-indigo-500"
                        onClick={handleGenerate}
                        disabled={loading || creating || !referenceCode.trim()}
                    >
                        {loading ? "Se generează..." : "1. Generează Ruleset"}
                    </button>
                    {ruleset && (
                        <button
                            className="px-6 py-2.5 bg-emerald-600 text-white font-semibold rounded-xl hover:bg-emerald-700 disabled:opacity-50 dark:bg-emerald-500"
                            onClick={handleCreate}
                            disabled={creating}
                        >
                            {creating ? "Se creează..." : "2. Creează Sesiunea"}
                        </button>
                    )}
                </div>

                {error && <p className="text-red-500 mt-4 text-sm font-semibold">{error}</p>}

                {ruleset && (
                    <div className="mt-6 p-4 rounded-xl bg-slate-100 dark:bg-slate-800 text-sm font-mono overflow-auto max-h-64 border border-slate-200 dark:border-slate-700 text-slate-800 dark:text-slate-200">
                        {JSON.stringify(ruleset, null, 2)}
                    </div>
                )}
            </div>
        </div>
    );
}

export function AdminLiveCodingPage() {
    const nav = useNavigate();
    const { code: codeParam } = useParams<{ code?: string }>();

    const [sessionCode, setSessionCode] = useState(codeParam ?? "");
    const [phase, setPhase] = useState<"pick" | "lobby" | "session">(codeParam ? "lobby" : "pick");
    const [showEndConfirm, setShowEndConfirm] = useState(false);

    const { state, startSession, endSession } = useLiveCodingSession(
        sessionCode,
        "Profesor",
        phase === "lobby" || phase === "session"
    );

    useEffect(() => {
        if (state.status === "running" && phase !== "session") setPhase("session");
        if (state.status === "ended") setPhase("session");
    }, [state.status, phase]);

    const handleCreate = (code: string) => {
        setSessionCode(code);
        setPhase("lobby");
        nav(`/app/coding-live/host/${code}`, { replace: true });
    };

    const handleEnd = async () => {
        setShowEndConfirm(false);
        await endSession();
    };

    if (phase === "pick") {
        return <CreateSessionStep onCreate={handleCreate} />;
    }

    if (state.status === "ended") {
        return (
            <div className="space-y-6 max-w-4xl mx-auto">
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

                <button onClick={() => { setPhase("pick"); setSessionCode(""); nav("/app/coding-live/host"); }}
                    className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500">
                    Lansează altă sesiune
                </button>
            </div>
        );
    }

    if (phase === "lobby" || state.status === "lobby") {
        return (
            <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px] max-w-6xl mx-auto">
                <div className="space-y-5">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex items-center justify-between">
                            <div>
                                <div className="flex items-center gap-2">
                                    <span className="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-amber-500" />
                                    <span className="text-lg font-bold text-slate-900 dark:text-white">Lobby — așteptare studenți</span>
                                </div>
                                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                                    {state.players.length} conectați
                                </p>
                            </div>
                            <button
                                onClick={startSession}
                                disabled={state.players.length === 0 || state.status === "connecting"}
                                className="rounded-2xl bg-emerald-600 px-6 py-3 text-sm font-bold text-white hover:bg-emerald-700 disabled:opacity-50 dark:bg-emerald-500 transition"
                            >
                                ▶ Start Coding
                            </button>
                        </div>
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <SessionCodeDisplay code={sessionCode} />
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">
                            Studenți în lobby
                        </div>
                        {state.players.length === 0 ? (
                            <div className="flex flex-col items-center py-8 text-center text-slate-500">
                                Așteptăm conexiuni...
                            </div>
                        ) : (
                            <div className="flex flex-wrap gap-2">
                                {state.players.map(p => (
                                    <span key={p.id} className="flex items-center gap-1.5 rounded-full border border-indigo-100 bg-indigo-50 px-3 py-1.5 text-sm font-medium text-indigo-700 dark:border-indigo-500/20 dark:bg-indigo-500/10 dark:text-indigo-300">
                                        <span className="h-2 w-2 rounded-full bg-emerald-500" />
                                        {p.displayName}
                                    </span>
                                ))}
                            </div>
                        )}
                    </div>
                </div>

                <aside>
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Clasament live</div>
                        <LeaderboardPanel entries={state.leaderboard} compact />
                    </div>
                </aside>
            </div>
        );
    }

    return (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_300px] max-w-6xl mx-auto">
            <div className="space-y-5">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55 flex justify-between items-center">
                    <div className="flex items-center gap-3">
                        <span className="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-red-500" />
                        <div>
                            <span className="text-sm font-bold text-slate-900 dark:text-white">Live Coding în desfășurare</span>
                            <div className="text-sm text-slate-500 dark:text-slate-400">Cod Sesiune: {sessionCode}</div>
                        </div>
                    </div>
                    <div className="flex items-center gap-6">
                        <CountdownTimer deadlineUtc={state.deadlineUtc} onExpire={handleEnd} />
                        <button
                            onClick={() => setShowEndConfirm(true)}
                            className="rounded-xl border border-red-200 bg-red-50 px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800/50 dark:bg-red-900/20 dark:text-red-300 transition"
                        >
                            Oprește
                        </button>
                    </div>
                </div>

                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55 min-h-[300px] flex items-center justify-center text-slate-500">
                    <div className="text-center">
                        <div className="text-4xl mb-3">👨‍💻</div>
                        <p>Studenții rezolvă sarcina acum.</p>
                        <p className="text-sm">Vei vedea actualizările în clasament imediat ce submisii corecte sunt trimise.</p>
                    </div>
                </div>
            </div>

            <aside className="space-y-4">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">🏆 Clasament live</div>
                    <LeaderboardPanel entries={state.leaderboard} />
                </div>
            </aside>

            {showEndConfirm && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm">
                    <div className="w-full max-w-sm rounded-3xl border border-slate-900/10 bg-white p-6 shadow-xl dark:border-white/10 dark:bg-slate-900">
                        <div className="text-lg font-bold text-slate-900 dark:text-white">Oprești sesiunea?</div>
                        <div className="mt-5 flex gap-3">
                            <button onClick={() => setShowEndConfirm(false)} className="flex-1 rounded-2xl border border-slate-200 py-2.5 text-sm dark:border-slate-700 dark:text-white">Anulează</button>
                            <button onClick={handleEnd} className="flex-1 rounded-2xl bg-red-600 text-white font-bold py-2.5 mt-0">Da, oprește</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
