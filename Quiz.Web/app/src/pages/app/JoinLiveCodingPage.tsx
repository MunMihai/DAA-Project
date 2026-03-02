import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import {
    useLiveCodingSession,
    type LeaderboardEntry,
} from "../../hooks/useLiveCodingSession";

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
            <div className="text-center">
                <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-3xl bg-indigo-600 text-3xl shadow-lg shadow-indigo-500/30 dark:bg-indigo-500">
                    👨‍💻
                </div>
                <h1 className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Intră în Coding Live
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

export function JoinLiveCodingPage() {
    const { code: codeParam } = useParams<{ code?: string }>();
    const nav = useNavigate();
    const { user } = useAuth();

    const [sessionCode, setSessionCode] = useState(codeParam?.toUpperCase() ?? "");
    const [joined, setJoined] = useState(() => {
        if (!codeParam) return false;
        return sessionStorage.getItem(`lc_joined_${codeParam.toUpperCase()}`) === "true";
    });
    const [displayName, setDisplayName] = useState(() => {
        const defaultName = user?.email?.split("@")[0] ?? "";
        if (!codeParam) return defaultName;
        return sessionStorage.getItem(`lc_name_${codeParam.toUpperCase()}`) || defaultName;
    });
    const [studentCode, setStudentCode] = useState(() => {
        const defaultCode = "public class Solution\n{\n    \n}";
        if (!codeParam) return defaultCode;
        return localStorage.getItem(`lc_code_${codeParam.toUpperCase()}`) || defaultCode;
    });
    const [myConnectionId] = useState(() => Math.random().toString(36).slice(2));

    const { state, submitCode } = useLiveCodingSession(
        sessionCode,
        displayName,
        joined,
    );

    const handleJoin = (code: string, name: string) => {
        const upperCode = code.toUpperCase();
        setSessionCode(upperCode);
        setDisplayName(name);
        setJoined(true);
        sessionStorage.setItem(`lc_joined_${upperCode}`, "true");
        sessionStorage.setItem(`lc_name_${upperCode}`, name);
        nav(`/app/coding-live/join/${upperCode}`, { replace: true });
    };

    const handleCodeChange = (e: any) => {
        const val = e.target.value;
        setStudentCode(val);
        if (sessionCode) {
            localStorage.setItem(`lc_code_${sessionCode}`, val);
        }
    };

    if (!joined) {
        return (
            <div className="flex min-h-[60vh] items-center justify-center px-4">
                <JoinForm defaultCode={codeParam ?? ""} onJoin={handleJoin} />
            </div>
        );
    }

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

    if (state.status === "error") {
        return (
            <div className="mx-auto max-w-sm space-y-4 pt-10 text-center">
                <div className="text-5xl">⚠️</div>
                <div className="text-lg font-bold text-slate-900 dark:text-white">Conexiune pierdută</div>
                <p className="text-sm text-slate-500 dark:text-slate-400">{state.error}</p>
                <button onClick={() => { setJoined(false); nav("/app/coding-live"); }}
                    className="rounded-2xl bg-indigo-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500">
                    Încearcă din nou
                </button>
            </div>
        );
    }

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
                        Aștepți ca profesorul să înceapă sarcina de programare.
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

    return (
        <div className="mx-auto max-w-5xl">
            <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_250px]">
                <div className="space-y-4">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55 flex justify-between items-center">
                        <div>
                            <span className="text-sm font-bold text-slate-900 dark:text-white">Live Coding</span>
                            <div className="text-xs text-slate-500 mt-1">Sesiune: {sessionCode}</div>
                        </div>
                        <div className="text-right flex items-center gap-4">
                            <CountdownTimer deadlineUtc={state.deadlineUtc} />
                            {state.lastAck && (
                                <span className={cn(
                                    "px-3 py-1 rounded-full text-sm font-bold",
                                    state.lastAck.passed ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400" : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400"
                                )}>
                                    Scor curent: {state.lastAck.yourScore}p
                                </span>
                            )}
                        </div>
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <textarea
                            className="w-full h-80 p-4 border rounded-xl font-mono text-sm bg-slate-50 text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 dark:bg-slate-950 dark:border-white/10 dark:text-slate-100 placeholder-slate-400"
                            value={studentCode}
                            onChange={handleCodeChange}
                            placeholder="Scrie codul C# aici..."
                        />
                        <button
                            onClick={() => submitCode(studentCode)}
                            className="w-full mt-4 rounded-xl bg-indigo-600 py-3 text-sm font-bold text-white transition hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                        >
                            Evaluează codul (trimite soluția)
                        </button>
                    </div>

                    {state.lastAck && (
                        <div className={cn(
                            "rounded-3xl border p-5 shadow-sm",
                            state.lastAck.passed
                                ? "border-emerald-200 bg-emerald-50 dark:border-emerald-900/50 dark:bg-emerald-900/10"
                                : "border-red-200 bg-red-50 dark:border-red-900/50 dark:bg-red-900/10"
                        )}>
                            <h3 className={cn("text-lg font-bold mb-2", state.lastAck.passed ? "text-emerald-700 dark:text-emerald-400" : "text-red-700 dark:text-red-400")}>
                                {state.lastAck.passed ? "Cod evaluat cu succes!" : "Eroare de validare sau compilare"}
                            </h3>
                            {!state.lastAck.passed && state.lastAck.violations?.length > 0 && (
                                <ul className="list-disc pl-5 mt-2 space-y-1">
                                    {state.lastAck.violations.map((v, i) => (
                                        <li key={i} className="text-sm text-red-800 dark:text-red-300">
                                            <strong>[{v.ruleId}]</strong> {v.message}
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </div>
                    )}
                </div>

                <aside className="space-y-4">
                    {state.leaderboard.length > 0 && (
                        <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                            <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">
                                Clasament Live
                            </div>
                            <MiniLeaderboard entries={state.leaderboard} myId={myConnectionId} />
                        </div>
                    )}
                </aside>
            </div>
        </div>
    );
}
