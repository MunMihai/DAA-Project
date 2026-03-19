import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import {
    useLiveCompileCodingSession,
    type CompileSolutionAck,
    type LeaderboardEntry,
} from "../../hooks/useLiveCompileCodingSession";

function cn(...xs: Array<string | false | null | undefined>) {
    return xs.filter(Boolean).join(" ");
}

function languageLabel(language: string) {
    const normalized = language.trim().toLowerCase();
    if (normalized === "csharp") return "C#";
    if (normalized === "python") return "Python";
    if (normalized === "javascript") return "JavaScript";
    return normalized || "Necunoscut";
}

function defaultTemplate(language: string) {
    const normalized = language.trim().toLowerCase();
    if (normalized === "python") {
        return "def solve():\n    pass\n\nif __name__ == '__main__':\n    solve()\n";
    }

    if (normalized === "javascript") {
        return "function solve() {\n}\n\nsolve();\n";
    }

    return [
        "using System;",
        "",
        "public static class Program",
        "{",
        "    public static void Main()",
        "    {",
        "    }",
        "}",
        "",
    ].join("\n");
}

function draftKey(sessionCode: string, taskId: string, language: string) {
    return `lcc_draft_${sessionCode}_${taskId}_${language}`;
}

function CountdownTimer({ deadlineUtc }: { deadlineUtc: Date | null }) {
    const [timeLeft, setTimeLeft] = useState(0);

    useEffect(() => {
        if (!deadlineUtc) return;

        const calc = () => Math.max(0, Math.floor((deadlineUtc.getTime() - Date.now()) / 1000));
        setTimeLeft(calc());

        const timer = window.setInterval(() => setTimeLeft(calc()), 1000);
        return () => window.clearInterval(timer);
    }, [deadlineUtc]);

    if (!deadlineUtc) return null;

    const minutes = Math.floor(timeLeft / 60).toString().padStart(2, "0");
    const seconds = (timeLeft % 60).toString().padStart(2, "0");

    return (
        <div className="rounded-2xl bg-red-50 px-4 py-2 font-mono text-lg font-black text-red-700 dark:bg-red-500/10 dark:text-red-300">
            {minutes}:{seconds}
        </div>
    );
}

function LeaderboardPanel({
    entries,
    currentDisplayName,
}: {
    entries: LeaderboardEntry[];
    currentDisplayName: string;
}) {
    const medals = ["🥇", "🥈", "🥉"];

    if (entries.length === 0) {
        return <div className="text-sm text-slate-500 dark:text-slate-400">Clasamentul va apărea după primele evaluări.</div>;
    }

    return (
        <div className="space-y-2">
            {entries.slice(0, 10).map((entry, index) => {
                const isMe = entry.displayName === currentDisplayName;
                return (
                    <div
                        key={`${entry.playerId}-${index}`}
                        className={cn(
                            "flex items-center justify-between rounded-2xl px-3 py-2 text-sm font-medium",
                            isMe
                                ? "bg-indigo-600 text-white"
                                : "bg-slate-50 text-slate-700 dark:bg-slate-950/40 dark:text-slate-200",
                        )}
                    >
                        <span className="flex min-w-0 items-center gap-2">
                            <span className="w-6 shrink-0 text-center">{medals[index] ?? `${index + 1}.`}</span>
                            <span className="truncate">{entry.displayName}</span>
                            {isMe && <span className="text-xs opacity-70">(tu)</span>}
                        </span>
                        <span className="ml-2 shrink-0 font-bold">{entry.score}p</span>
                    </div>
                );
            })}
        </div>
    );
}

function JoinForm({
    defaultCode,
    defaultName,
    onJoin,
}: {
    defaultCode: string;
    defaultName: string;
    onJoin: (code: string, name: string) => void;
}) {
    const { user } = useAuth();
    const [code, setCode] = useState(defaultCode);
    const [name, setName] = useState(defaultName || (user?.email?.split("@")[0] ?? ""));
    const [error, setError] = useState("");

    const handleSubmit = () => {
        const normalizedCode = code.trim().toUpperCase();
        const normalizedName = name.trim();

        if (!/^[A-Z0-9]{6}$/.test(normalizedCode)) {
            setError("Codul trebuie să aibă exact 6 caractere alfanumerice.");
            return;
        }

        if (!normalizedName) {
            setError("Introdu un nume de afișare.");
            return;
        }

        if (normalizedName.length > 50) {
            setError("Numele de afișare poate avea cel mult 50 de caractere.");
            return;
        }

        onJoin(normalizedCode, normalizedName);
    };

    return (
        <div className="mx-auto max-w-sm space-y-6">
            <div className="text-center">
                <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-3xl bg-indigo-600 text-3xl shadow-lg shadow-indigo-500/30 dark:bg-indigo-500">
                    ⚙️
                </div>
                <h1 className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Intră în Live Coding (Compile)
                </h1>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                    Introdu codul primit de la profesor pentru testul de compilare.
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
                            onChange={(event) => { setCode(event.target.value.toUpperCase()); setError(""); }}
                            onKeyDown={(event) => event.key === "Enter" && handleSubmit()}
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
                            onChange={(event) => { setName(event.target.value); setError(""); }}
                            onKeyDown={(event) => event.key === "Enter" && handleSubmit()}
                            placeholder="Cum vrei să apari în clasament"
                            maxLength={50}
                            className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3.5 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-500/20 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        />
                    </div>

                    {error && (
                        <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                            {error}
                        </div>
                    )}

                    <button
                        onClick={handleSubmit}
                        className="w-full rounded-2xl bg-indigo-600 py-3.5 text-sm font-bold text-white shadow-sm shadow-indigo-500/20 transition hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                    >
                        Intră în sesiune →
                    </button>
                </div>
            </div>
        </div>
    );
}

function SubmissionFeedback({
    ack,
}: {
    ack: CompileSolutionAck;
}) {
    return (
        <div className={cn(
            "rounded-3xl border p-5 shadow-sm",
            ack.passed
                ? "border-emerald-200 bg-emerald-50 dark:border-emerald-500/20 dark:bg-emerald-500/10"
                : "border-red-200 bg-red-50 dark:border-red-500/20 dark:bg-red-500/10",
        )}>
            <div className={cn(
                "text-lg font-bold",
                ack.passed ? "text-emerald-700 dark:text-emerald-300" : "text-red-700 dark:text-red-300",
            )}>
                {ack.passed ? "Soluția a trecut evaluarea" : "Soluția a fost evaluată cu erori"}
            </div>

            <div className="mt-3 flex flex-wrap gap-2 text-xs">
                <span className="rounded-full bg-white/80 px-2.5 py-1 font-semibold text-slate-700 dark:bg-slate-950/40 dark:text-slate-200">
                    Teste trecute: {ack.passedCaseCount}/{ack.totalCaseCount}
                </span>
                <span className="rounded-full bg-white/80 px-2.5 py-1 font-semibold text-slate-700 dark:bg-slate-950/40 dark:text-slate-200">
                    Scor task: {ack.bestTaskScore}p
                </span>
                <span className="rounded-full bg-white/80 px-2.5 py-1 font-semibold text-slate-700 dark:bg-slate-950/40 dark:text-slate-200">
                    Delta: {ack.scoreDelta >= 0 ? "+" : ""}{ack.scoreDelta}p
                </span>
                <span className="rounded-full bg-white/80 px-2.5 py-1 font-semibold text-slate-700 dark:bg-slate-950/40 dark:text-slate-200">
                    Total: {ack.totalScore}p
                </span>
            </div>

            {ack.compileError && (
                <div className="mt-4 rounded-2xl bg-slate-950 px-4 py-3 text-sm text-red-300">
                    <div className="mb-2 font-semibold text-red-200">Compile error</div>
                    <pre className="overflow-auto whitespace-pre-wrap">{ack.compileError}</pre>
                </div>
            )}

            {ack.runtimeError && (
                <div className="mt-4 rounded-2xl bg-slate-950 px-4 py-3 text-sm text-orange-300">
                    <div className="mb-2 font-semibold text-orange-200">Runtime error</div>
                    <pre className="overflow-auto whitespace-pre-wrap">{ack.runtimeError}</pre>
                </div>
            )}

            <div className="mt-4 space-y-3">
                {ack.cases.map((item, index) => (
                    <div key={`${ack.taskId}-${index}`} className="rounded-2xl border border-white/60 bg-white/70 p-4 dark:border-white/10 dark:bg-slate-950/40">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                                Caz {index + 1} {item.isExample ? "(exemplu)" : ""}
                            </div>
                            <span className={item.passed
                                ? "rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
                                : "rounded-full bg-red-100 px-2 py-1 text-xs font-semibold text-red-700 dark:bg-red-500/10 dark:text-red-300"}>
                                {item.passed ? "Passed" : "Failed"}
                            </span>
                        </div>
                        <div className="mt-3 grid gap-3 lg:grid-cols-3">
                            <div>
                                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Input</div>
                                <pre className="rounded-xl bg-slate-950 px-3 py-2 text-xs text-slate-100">{item.input || "(gol)"}</pre>
                            </div>
                            <div>
                                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Output așteptat</div>
                                <pre className="rounded-xl bg-slate-950 px-3 py-2 text-xs text-slate-100">{item.expectedOutput || "(gol)"}</pre>
                            </div>
                            <div>
                                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Output primit</div>
                                <pre className="rounded-xl bg-slate-950 px-3 py-2 text-xs text-slate-100">{item.actualOutput || "(gol)"}</pre>
                            </div>
                        </div>
                        {item.errorMessage && (
                            <div className="mt-3 text-sm text-red-700 dark:text-red-300">{item.errorMessage}</div>
                        )}
                    </div>
                ))}
            </div>
        </div>
    );
}

export function JoinLiveCompileCodingPage() {
    const { code: codeParam } = useParams<{ code?: string }>();
    const nav = useNavigate();
    const { user } = useAuth();

    const [sessionCode, setSessionCode] = useState(codeParam?.toUpperCase() ?? "");
    const [joined, setJoined] = useState(() => {
        if (!codeParam) return false;
        return sessionStorage.getItem(`lcc_joined_${codeParam.toUpperCase()}`) === "true";
    });
    const [displayName, setDisplayName] = useState(() => {
        const defaultName = user?.email?.split("@")[0] ?? "";
        if (!codeParam) return defaultName;
        return sessionStorage.getItem(`lcc_name_${codeParam.toUpperCase()}`) || defaultName;
    });
    const [hasEstablishedSession, setHasEstablishedSession] = useState(false);
    const [selectedTaskId, setSelectedTaskId] = useState("");
    const [selectedLanguage, setSelectedLanguage] = useState("csharp");
    const [sourceCode, setSourceCode] = useState(defaultTemplate("csharp"));

    const { state, submitSolution } = useLiveCompileCodingSession(
        sessionCode,
        displayName,
        joined,
    );

    const selectedTask = state.tasks.find((task) => task.id === selectedTaskId) ?? state.tasks[0] ?? null;
    const currentAck = state.lastAck;

    const handleJoin = (code: string, name: string) => {
        setSessionCode(code);
        setDisplayName(name);
        setHasEstablishedSession(false);
        setJoined(true);
        sessionStorage.setItem(`lcc_joined_${code}`, "true");
        sessionStorage.setItem(`lcc_name_${code}`, name);
        nav(`/app/coding-compile-live/join/${code}`, { replace: true });
    };

    useEffect(() => {
        if (state.status === "lobby" || state.status === "running" || state.status === "ended") {
            setHasEstablishedSession(true);
        }
    }, [state.status]);

    useEffect(() => {
        if (!joined) {
            setHasEstablishedSession(false);
            return;
        }

        if (!hasEstablishedSession && state.status === "error") {
            sessionStorage.removeItem(`lcc_joined_${sessionCode}`);
            setJoined(false);
        }
    }, [joined, hasEstablishedSession, state.status, sessionCode]);

    useEffect(() => {
        if (state.allowedLanguages.length === 0) return;
        if (!state.allowedLanguages.includes(selectedLanguage)) {
            setSelectedLanguage(state.allowedLanguages[0]);
        }
    }, [state.allowedLanguages, selectedLanguage]);

    useEffect(() => {
        if (state.tasks.length === 0) return;
        if (!state.tasks.some((task) => task.id === selectedTaskId)) {
            setSelectedTaskId(state.tasks[0].id);
        }
    }, [state.tasks, selectedTaskId]);

    useEffect(() => {
        if (!sessionCode || !selectedTaskId || !selectedLanguage) return;
        const next = localStorage.getItem(draftKey(sessionCode, selectedTaskId, selectedLanguage))
            ?? defaultTemplate(selectedLanguage);
        setSourceCode(next);
    }, [sessionCode, selectedTaskId, selectedLanguage]);

    const handleCodeChange = (value: string) => {
        setSourceCode(value);
        if (!sessionCode || !selectedTaskId || !selectedLanguage) return;
        localStorage.setItem(draftKey(sessionCode, selectedTaskId, selectedLanguage), value);
    };

    const handleSubmit = () => {
        if (!selectedTask) return;
        submitSolution(selectedTask.id, selectedLanguage, sourceCode);
    };

    if (!joined) {
        return (
            <div className="flex min-h-[60vh] items-center justify-center px-4">
                <JoinForm defaultCode={codeParam ?? ""} defaultName={displayName} onJoin={handleJoin} />
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
                <button
                    onClick={() => { setJoined(false); nav("/app/coding-compile-live"); }}
                    className="rounded-2xl bg-indigo-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                >
                    Încearcă din nou
                </button>
            </div>
        );
    }

    if (state.status === "ended") {
        const myEntry = state.leaderboard.find((entry) => entry.displayName === displayName);
        const myRank = state.leaderboard.findIndex((entry) => entry.displayName === displayName) + 1;

        return (
            <div className="mx-auto max-w-4xl space-y-6 py-6">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 text-center shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-6xl">
                        {myRank === 1 ? "🏆" : myRank === 2 ? "🥈" : myRank === 3 ? "🥉" : "🎉"}
                    </div>
                    <div className="mt-3 text-2xl font-extrabold text-slate-900 dark:text-white">
                        {myRank > 0 ? `Locul ${myRank}` : "Sesiunea s-a încheiat"}
                    </div>
                    <div className="mt-2 text-sm text-slate-500 dark:text-slate-400">{state.title || "Live Coding (Compile)"}</div>
                    {myEntry && (
                        <div className="mt-3 text-3xl font-black text-indigo-600 dark:text-indigo-400">
                            {myEntry.score} puncte
                        </div>
                    )}
                </div>

                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="mb-4 text-sm font-semibold text-slate-700 dark:text-slate-300">Clasament final</div>
                    <LeaderboardPanel entries={state.leaderboard} currentDisplayName={displayName} />
                </div>

                <button
                    onClick={() => nav("/app")}
                    className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-bold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                >
                    Înapoi la Dashboard
                </button>
            </div>
        );
    }

    if (state.status === "lobby") {
        return (
            <div className="mx-auto max-w-2xl space-y-6 py-8 text-center">
                <div className="rounded-3xl border border-slate-900/10 bg-white p-8 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-5xl">⚙️</div>
                    <div className="mt-4 text-xl font-extrabold text-slate-900 dark:text-white">
                        Ești în lobby
                    </div>
                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        Bun venit, <strong className="text-slate-800 dark:text-slate-200">{displayName}</strong>. Profesorul va porni în curând sesiunea de coding compile.
                    </p>

                    <div className="mt-6 grid gap-3 sm:grid-cols-3">
                        <div className="rounded-2xl bg-slate-50 px-4 py-3 dark:bg-slate-950/40">
                            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Test</div>
                            <div className="mt-1 text-sm font-semibold text-slate-900 dark:text-slate-100">{state.title || "Live Coding (Compile)"}</div>
                        </div>
                        <div className="rounded-2xl bg-slate-50 px-4 py-3 dark:bg-slate-950/40">
                            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Sarcini</div>
                            <div className="mt-1 text-sm font-semibold text-slate-900 dark:text-slate-100">{state.tasks.length}</div>
                        </div>
                        <div className="rounded-2xl bg-slate-50 px-4 py-3 dark:bg-slate-950/40">
                            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Limbaje</div>
                            <div className="mt-1 text-sm font-semibold text-slate-900 dark:text-slate-100">
                                {state.allowedLanguages.map(languageLabel).join(", ") || "—"}
                            </div>
                        </div>
                    </div>
                </div>

                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-sm font-semibold text-slate-700 dark:text-slate-300">
                        {state.players.length} participanți conectați
                    </div>
                    {state.players.length > 0 && (
                        <div className="mt-4 flex flex-wrap justify-center gap-2">
                            {state.players.map((player) => (
                                <span
                                    key={player.id}
                                    className={cn(
                                        "rounded-full px-3 py-1.5 text-sm font-medium",
                                        player.displayName === displayName
                                            ? "bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300"
                                            : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
                                    )}
                                >
                                    {player.displayName}
                                </span>
                            ))}
                        </div>
                    )}
                </div>
            </div>
        );
    }

    return (
        <div className="mx-auto max-w-7xl space-y-5">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                        <div className="text-sm font-bold text-slate-900 dark:text-white">{state.title || "Live Coding (Compile)"}</div>
                        <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                            Sesiune: {sessionCode} • {state.tasks.length} sarcini
                        </div>
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                            Scor total: {state.leaderboard.find((entry) => entry.displayName === displayName)?.score ?? 0}p
                        </span>
                        <CountdownTimer deadlineUtc={state.deadlineUtc} />
                    </div>
                </div>
            </div>

            <div className="grid grid-cols-1 gap-5 xl:grid-cols-[320px_1fr_300px]">
                <aside className="space-y-4">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Sarcini</div>
                        <div className="space-y-2">
                            {state.tasks.map((task, index) => (
                                <button
                                    key={task.id}
                                    onClick={() => setSelectedTaskId(task.id)}
                                    className={cn(
                                        "w-full rounded-2xl border px-4 py-3 text-left transition",
                                        task.id === selectedTask?.id
                                            ? "border-indigo-500 bg-indigo-50 text-indigo-700 dark:border-indigo-400 dark:bg-indigo-500/10 dark:text-indigo-300"
                                            : "border-slate-200 bg-slate-50 text-slate-700 hover:bg-slate-100 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200 dark:hover:bg-slate-950/50",
                                    )}
                                >
                                    <div className="text-xs font-semibold uppercase tracking-wide opacity-70">Task {index + 1}</div>
                                    <div className="mt-1 text-sm font-semibold">{task.title}</div>
                                    <div className="mt-2 flex items-center justify-between text-xs">
                                        <span>{task.points}p</span>
                                        <span>Scor: {state.playerTaskScores[task.id] ?? 0}p</span>
                                    </div>
                                </button>
                            ))}
                        </div>
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Limbaj</div>
                        <div className="flex flex-wrap gap-2">
                            {state.allowedLanguages.map((language) => (
                                <button
                                    key={language}
                                    onClick={() => setSelectedLanguage(language)}
                                    className={cn(
                                        "rounded-full px-3 py-1.5 text-sm font-semibold transition",
                                        selectedLanguage === language
                                            ? "bg-indigo-600 text-white dark:bg-indigo-500"
                                            : "border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200",
                                    )}
                                >
                                    {languageLabel(language)}
                                </button>
                            ))}
                        </div>
                    </div>
                </aside>

                <div className="space-y-5">
                    {selectedTask && (
                        <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                                <div>
                                    <div className="text-xl font-bold text-slate-900 dark:text-white">{selectedTask.title}</div>
                                    <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">{selectedTask.points} puncte</div>
                                </div>
                                <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                                    Limbaj activ: {languageLabel(selectedLanguage)}
                                </span>
                            </div>

                            <div className="mt-6 grid gap-4 lg:grid-cols-3">
                                <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-950/40 lg:col-span-3">
                                    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Condiția problemei</div>
                                    <div className="whitespace-pre-wrap text-sm text-slate-700 dark:text-slate-200">{selectedTask.problemStatement}</div>
                                </div>
                                <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-950/40">
                                    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Date de intrare</div>
                                    <div className="whitespace-pre-wrap text-sm text-slate-700 dark:text-slate-200">{selectedTask.inputDescription}</div>
                                </div>
                                <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-950/40">
                                    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Date de ieșire</div>
                                    <div className="whitespace-pre-wrap text-sm text-slate-700 dark:text-slate-200">{selectedTask.outputDescription}</div>
                                </div>
                                <div className="rounded-2xl bg-slate-50 p-4 dark:bg-slate-950/40">
                                    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Exemplu</div>
                                    <div className="space-y-3">
                                        <div>
                                            <div className="mb-1 text-xs font-semibold text-slate-500 dark:text-slate-400">Input</div>
                                            <pre className="rounded-xl bg-slate-950 px-3 py-2 text-xs text-slate-100">{selectedTask.exampleInput || "(gol)"}</pre>
                                        </div>
                                        <div>
                                            <div className="mb-1 text-xs font-semibold text-slate-500 dark:text-slate-400">Output</div>
                                            <pre className="rounded-xl bg-slate-950 px-3 py-2 text-xs text-slate-100">{selectedTask.exampleOutput || "(gol)"}</pre>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                            <div>
                                <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">Editor soluție</div>
                                <div className="text-xs text-slate-500 dark:text-slate-400">
                                    Fiecare trimitere compilează și verifică output-ul pentru sarcina selectată.
                                </div>
                            </div>
                            <button
                                onClick={handleSubmit}
                                disabled={!selectedTask}
                                className="rounded-2xl bg-indigo-600 px-5 py-3 text-sm font-bold text-white transition hover:bg-indigo-700 disabled:opacity-50 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                            >
                                Compilează și trimite
                            </button>
                        </div>

                        <textarea
                            value={sourceCode}
                            onChange={(event) => handleCodeChange(event.target.value)}
                            spellCheck={false}
                            className="mt-4 h-[420px] w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-4 font-mono text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950 dark:text-slate-100"
                            placeholder={`Scrie soluția în ${languageLabel(selectedLanguage)}...`}
                        />
                    </div>

                    {currentAck && <SubmissionFeedback ack={currentAck} />}
                </div>

                <aside className="space-y-4">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Clasament live</div>
                        <LeaderboardPanel entries={state.leaderboard} currentDisplayName={displayName} />
                    </div>
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 text-sm text-slate-600 shadow-sm dark:border-white/10 dark:bg-slate-900/55 dark:text-slate-300">
                        Poți retrimite soluția de mai multe ori. Se păstrează cel mai bun scor obținut pe fiecare sarcină.
                    </div>
                </aside>
            </div>
        </div>
    );
}
