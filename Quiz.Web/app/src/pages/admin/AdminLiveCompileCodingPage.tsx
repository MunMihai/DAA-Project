import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
    compileCodingApi,
    type CompileCodingTask,
    type CompileCodingTemplate,
    type CreateCompileCodingSessionRequest,
    type CreateCompileCodingTaskRequest,
} from "../../api/compileCodingApi";
import { useApi } from "../../api/axios";
import { useLiveCompileCodingSession, type LeaderboardEntry } from "../../hooks/useLiveCompileCodingSession";
import { toastApiError, toastErrorMessage } from "../../utils/toastError";

function cn(...xs: (string | false | null | undefined)[]) {
    return xs.filter(Boolean).join(" ");
}

function LeaderboardPanel({ entries, compact = false }: { entries: LeaderboardEntry[]; compact?: boolean }) {
    const medals = ["🥇", "🥈", "🥉"];
    if (entries.length === 0) {
        return <p className="text-sm text-slate-400 dark:text-slate-500 italic">Niciun punct încă.</p>;
    }

    return (
        <ol className="space-y-1.5">
            {entries.slice(0, compact ? 5 : 20).map((entry, index) => (
                <li
                    key={entry.playerId}
                    className={cn(
                        "flex items-center justify-between rounded-xl px-3 py-2 text-sm font-medium transition-all",
                        index === 0 && "bg-amber-50 text-amber-900 ring-1 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-200 dark:ring-amber-500/20",
                        index === 1 && "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
                        index === 2 && "bg-orange-50 text-orange-800 dark:bg-orange-500/10 dark:text-orange-200",
                        index > 2 && "text-slate-600 dark:text-slate-400",
                    )}
                >
                    <span className="flex min-w-0 items-center gap-2">
                        <span className="w-6 shrink-0 text-center">{medals[index] ?? `${index + 1}`}</span>
                        <span className="truncate">{entry.displayName}</span>
                    </span>
                    <span className="ml-2 shrink-0 font-bold tabular-nums">{entry.score}p</span>
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
            <button
                onClick={copy}
                className="rounded-xl border border-slate-200 bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200"
            >
                {copied ? "✓ Copiat!" : "Copiază codul"}
            </button>
            <p className="text-center text-xs text-slate-500 dark:text-slate-400">
                Studenții accesează <span className="font-semibold text-slate-700 dark:text-slate-200">/app/coding-compile-live</span> și introduc codul
            </p>
        </div>
    );
}

type EditableTask = CreateCompileCodingTaskRequest;

const LANGUAGE_OPTIONS = [
    { value: "csharp", label: "C#" },
    { value: "python", label: "Python" },
    { value: "javascript", label: "JavaScript" },
];

function emptyTask(index: number): EditableTask {
    return {
        id: `task-${Date.now()}-${index}`,
        title: `Sarcina ${index + 1}`,
        problemStatement: "",
        inputDescription: "",
        outputDescription: "",
        exampleInput: "",
        exampleOutput: "",
        points: 100,
    };
}
function cloneTask(task: EditableTask, index: number): EditableTask {
    return {
        id: task.id ?? `task-${Date.now()}-${index}`,
        title: task.title,
        problemStatement: task.problemStatement,
        inputDescription: task.inputDescription,
        outputDescription: task.outputDescription,
        exampleInput: task.exampleInput,
        exampleOutput: task.exampleOutput,
        points: task.points,
    };
}

function toEditableTasks(template: CompileCodingTemplate): EditableTask[] {
    return template.tasks.map((task, index) => cloneTask({
        id: `${task.id}-${Date.now()}-${index}`,
        title: task.title,
        problemStatement: task.problemStatement,
        inputDescription: task.inputDescription,
        outputDescription: task.outputDescription,
        exampleInput: task.exampleInput,
        exampleOutput: task.exampleOutput,
        points: task.points,
    }, index));
}

function buildSessionPayload(input: {
    title: string;
    timeLimitMinutes: number;
    allowedLanguages: string[];
    tasks: EditableTask[];
}): { payload?: CreateCompileCodingSessionRequest; error?: string } {
    if (!input.title.trim()) return { error: "Titlul testului este obligatoriu." };
    if (input.allowedLanguages.length === 0) return { error: "Selectează cel puțin un limbaj permis." };
    if (input.tasks.length === 0) return { error: "Adaugă cel puțin o sarcină." };

    if (input.tasks.some(task =>
        !task.title.trim()
        || !task.problemStatement.trim()
        || !task.inputDescription.trim()
        || !task.outputDescription.trim()
    )) {
        return { error: "Completează toate câmpurile obligatorii pentru fiecare sarcină." };
    }

    return {
        payload: {
            title: input.title.trim(),
            timeLimitSeconds: input.timeLimitMinutes * 60,
            allowedLanguages: [...new Set(input.allowedLanguages.map(language => language.trim().toLowerCase()).filter(Boolean))],
            tasks: input.tasks.map((task) => ({
                id: task.id,
                title: task.title.trim(),
                problemStatement: task.problemStatement.trim(),
                inputDescription: task.inputDescription.trim(),
                outputDescription: task.outputDescription.trim(),
                exampleInput: task.exampleInput,
                exampleOutput: task.exampleOutput,
                points: task.points,
            })),
        },
    };
}

function formatTemplateDate(value: string) {
    return new Intl.DateTimeFormat("ro-RO", {
        dateStyle: "medium",
        timeStyle: "short",
    }).format(new Date(value));
}

function CreateSessionStep({ onCreate }: { onCreate: (code: string) => void }) {
    const api = useApi();
    const compileApi = useMemo(() => compileCodingApi(api), [api]);

    const [title, setTitle] = useState("Live Coding (Compile)");
    const [timeLimitMinutes, setTimeLimitMinutes] = useState(15);
    const [allowedLanguages, setAllowedLanguages] = useState<string[]>(["csharp"]);
    const [tasks, setTasks] = useState<EditableTask[]>([emptyTask(0)]);
    const [templates, setTemplates] = useState<CompileCodingTemplate[]>([]);
    const [templatesLoading, setTemplatesLoading] = useState(true);
    const [templatesError, setTemplatesError] = useState<string | null>(null);
    const [creating, setCreating] = useState(false);
    const [creatingTemplateSlug, setCreatingTemplateSlug] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;

        const loadTemplates = async () => {
            setTemplatesLoading(true);
            setTemplatesError(null);

            try {
                const data = await compileApi.getTemplates();
                if (!cancelled) setTemplates(data);
            } catch {
                if (!cancelled) {
                    setTemplates([]);
                    setTemplatesError("Nu pot încărca șabloanele salvate.");
                }
            } finally {
                if (!cancelled) setTemplatesLoading(false);
            }
        };

        loadTemplates();
        return () => { cancelled = true; };
    }, [compileApi]);

    const updateTask = (taskId: string | null | undefined, patch: Partial<EditableTask>) => {
        setTasks(prev => prev.map(task => task.id === taskId ? { ...task, ...patch } : task));
    };

    const addTask = () => {
        setTasks(prev => [...prev, emptyTask(prev.length)]);
    };

    const removeTask = (taskId: string | null | undefined) => {
        setTasks(prev => prev.length === 1 ? prev : prev.filter(task => task.id !== taskId));
    };

    const toggleLanguage = (language: string) => {
        setAllowedLanguages(prev => (
            prev.includes(language)
                ? prev.filter(item => item !== language)
                : [...prev, language]
        ));
    };

    const createSession = async (payload: CreateCompileCodingSessionRequest) => {
        setCreating(true);
        setError(null);
        try {
            const response = await compileApi.createSession(payload);
            onCreate(response.sessionCode);
        } catch (err: any) {
            setError(err?.response?.data?.message ?? "Nu pot crea sesiunea compile.");
            toastApiError(err, "Nu pot crea sesiunea compile.");
        } finally {
            setCreating(false);
            setCreatingTemplateSlug(null);
        }
    };

    const handleCreate = async () => {
        const result = buildSessionPayload({
            title,
            timeLimitMinutes,
            allowedLanguages,
            tasks,
        });

        if (!result.payload) {
            setError(result.error ?? "Datele sesiunii sunt invalide.");
            toastErrorMessage(result.error ?? "Datele sesiunii sunt invalide.");
            return;
        }

        await createSession(result.payload);
    };

    const handleUseTemplate = (template: CompileCodingTemplate) => {
        setTitle(template.title);
        setTimeLimitMinutes(Math.max(1, Math.ceil(template.suggestedTimeLimitSeconds / 60)));
        setAllowedLanguages(template.allowedLanguages.length > 0 ? [...template.allowedLanguages] : ["csharp"]);
        setTasks(toEditableTasks(template));
        setError(null);
    };

    const handleCreateFromTemplate = async (template: CompileCodingTemplate) => {
        const result = buildSessionPayload({
            title: template.title,
            timeLimitMinutes: Math.max(1, Math.ceil(template.suggestedTimeLimitSeconds / 60)),
            allowedLanguages: template.allowedLanguages,
            tasks: toEditableTasks(template),
        });

        if (!result.payload) {
            setError(result.error ?? "Șablonul selectat este invalid.");
            toastErrorMessage(result.error ?? "Șablonul selectat este invalid.");
            return;
        }

        setCreatingTemplateSlug(template.slug);
        await createSession(result.payload);
    };

    return (
        <div className="space-y-6">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div>
                        <div className="text-xl font-bold text-slate-900 dark:text-white">Șabloane salvate</div>
                        <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                            Sesiunile `Live Coding (Compile)` se salvează automat aici și pot fi refolosite oricând pentru a porni teste noi cu aceleași sarcini.
                        </p>
                    </div>
                    <span className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300">
                        {templates.length} șabloane
                    </span>
                </div>

                {templatesLoading ? (
                    <div className="mt-5 text-sm text-slate-500 dark:text-slate-400">Se încarcă șabloanele salvate...</div>
                ) : templatesError ? (
                    <div className="mt-5 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
                        {templatesError}
                    </div>
                ) : templates.length === 0 ? (
                    <div className="mt-5 rounded-2xl border border-dashed border-slate-200 px-4 py-5 text-sm text-slate-500 dark:border-white/10 dark:text-slate-400">
                        Nu există încă șabloane salvate. Creează prima sesiune, iar setul de sarcini va apărea aici pentru reutilizare.
                    </div>
                ) : (
                    <div className="mt-5 space-y-3">
                        {templates.map((template) => (
                            <div key={template.id || template.slug} className="rounded-2xl border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-slate-950/30">
                                <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                                    <div className="min-w-0 flex-1">
                                        <div className="flex flex-wrap items-center gap-2">
                                            <div className="text-lg font-semibold text-slate-900 dark:text-slate-100">{template.title}</div>
                                            <span className="rounded-full bg-slate-200 px-2 py-1 text-[11px] font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                                                {template.tasks.length} sarcini
                                            </span>
                                        </div>
                                        <div className="mt-2 text-sm text-slate-500 dark:text-slate-400">{template.description}</div>
                                        <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-500 dark:text-slate-400">
                                            <span className="rounded-full bg-white px-2.5 py-1 dark:bg-slate-900/60">
                                                {Math.ceil(template.suggestedTimeLimitSeconds / 60)} minute
                                            </span>
                                            <span className="rounded-full bg-white px-2.5 py-1 dark:bg-slate-900/60">
                                                {template.allowedLanguages.join(", ")}
                                            </span>
                                            <span className="rounded-full bg-white px-2.5 py-1 dark:bg-slate-900/60">
                                                Actualizat: {formatTemplateDate(template.updatedAt)}
                                            </span>
                                        </div>
                                        <div className="mt-3 flex flex-wrap gap-2">
                                            {template.tasks.slice(0, 4).map((task) => (
                                                <span key={`${template.slug}-${task.id}`} className="rounded-full bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300">
                                                    {task.title}
                                                </span>
                                            ))}
                                        </div>
                                    </div>

                                    <div className="flex flex-wrap gap-2 xl:justify-end">
                                        <button
                                            onClick={() => handleUseTemplate(template)}
                                            disabled={creating}
                                            className="rounded-2xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-200"
                                        >
                                            Folosește în formular
                                        </button>
                                        <button
                                            onClick={() => handleCreateFromTemplate(template)}
                                            disabled={creating}
                                            className="rounded-2xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50 dark:bg-indigo-500"
                                        >
                                            {creating && creatingTemplateSlug === template.slug ? "Se creează..." : "Deschide sesiune"}
                                        </button>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Lansează Live Coding (Compile)
                </div>
                <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                    Configurează un test nou sau pornește direct dintr-un șablon salvat anterior.
                </p>

                <div className="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-[2fr_180px]">
                    <div>
                        <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Titlul testului
                        </label>
                        <input
                            value={title}
                            onChange={e => setTitle(e.target.value)}
                            className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        />
                    </div>
                    <div>
                        <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Timp (minute)
                        </label>
                        <input
                            type="number"
                            min={1}
                            max={120}
                            value={timeLimitMinutes}
                            onChange={e => setTimeLimitMinutes(Math.max(1, parseInt(e.target.value, 10) || 15))}
                            className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                        />
                    </div>
                </div>

                <div className="mt-5">
                    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                        Limbaje permise
                    </div>
                    <div className="flex flex-wrap gap-2">
                        {LANGUAGE_OPTIONS.map(option => (
                            <button
                                key={option.value}
                                onClick={() => toggleLanguage(option.value)}
                                className={cn(
                                    "rounded-full px-3 py-1.5 text-sm font-semibold transition",
                                    allowedLanguages.includes(option.value)
                                        ? "bg-indigo-600 text-white dark:bg-indigo-500"
                                        : "border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200"
                                )}
                            >
                                {option.label}
                            </button>
                        ))}
                    </div>
                </div>
            </div>

            <div className="space-y-4">
                {tasks.map((task, index) => (
                    <div key={task.id ?? index} className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-4 flex items-center justify-between gap-3">
                            <div className="text-lg font-bold text-slate-900 dark:text-white">Sarcina {index + 1}</div>
                            <button
                                onClick={() => removeTask(task.id)}
                                disabled={tasks.length === 1}
                                className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-100 disabled:opacity-40 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300"
                            >
                                Șterge
                            </button>
                        </div>

                        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_120px]">
                            <div>
                                <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Titlu sarcină
                                </label>
                                <input
                                    value={task.title}
                                    onChange={e => updateTask(task.id, { title: e.target.value })}
                                    className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                />
                            </div>
                            <div>
                                <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Puncte
                                </label>
                                <input
                                    type="number"
                                    min={10}
                                    step={10}
                                    value={task.points}
                                    onChange={e => updateTask(task.id, { points: Math.max(10, parseInt(e.target.value, 10) || 100) })}
                                    className="w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                />
                            </div>
                        </div>

                        <div className="mt-4">
                            <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                Condiția problemei
                            </label>
                            <textarea
                                value={task.problemStatement}
                                onChange={e => updateTask(task.id, { problemStatement: e.target.value })}
                                className="h-36 w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                            />
                        </div>

                        <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
                            <div>
                                <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Date de intrare
                                </label>
                                <textarea
                                    value={task.inputDescription}
                                    onChange={e => updateTask(task.id, { inputDescription: e.target.value })}
                                    className="h-28 w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                />
                            </div>
                            <div>
                                <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Date de ieșire
                                </label>
                                <textarea
                                    value={task.outputDescription}
                                    onChange={e => updateTask(task.id, { outputDescription: e.target.value })}
                                    className="h-28 w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                />
                            </div>
                        </div>

                        <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
                            <div>
                                <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Exemplu input
                                </label>
                                <textarea
                                    value={task.exampleInput}
                                    onChange={e => updateTask(task.id, { exampleInput: e.target.value })}
                                    className="h-28 w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 font-mono text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                />
                            </div>
                            <div>
                                <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Exemplu output
                                </label>
                                <textarea
                                    value={task.exampleOutput}
                                    onChange={e => updateTask(task.id, { exampleOutput: e.target.value })}
                                    className="h-28 w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 font-mono text-sm text-slate-900 outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                                />
                            </div>
                        </div>
                    </div>
                ))}
            </div>

            <div className="flex flex-wrap gap-3">
                <button
                    onClick={addTask}
                    className="rounded-2xl border border-slate-200 bg-white px-5 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200"
                >
                    + Adaugă sarcină
                </button>
                <button
                    onClick={handleCreate}
                    disabled={creating}
                    className="rounded-2xl bg-indigo-600 px-6 py-3 text-sm font-bold text-white hover:bg-indigo-700 disabled:opacity-50 dark:bg-indigo-500"
                >
                    {creating && !creatingTemplateSlug ? "Se creează..." : "Creează sesiunea"}
                </button>
            </div>

            {error && (
                <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
                    {error}
                </div>
            )}
        </div>
    );
}

export function AdminLiveCompileCodingPage() {
    const nav = useNavigate();
    const { code: codeParam } = useParams<{ code?: string }>();

    const [sessionCode, setSessionCode] = useState(codeParam ?? "");
    const [phase, setPhase] = useState<"pick" | "lobby" | "session">(codeParam ? "lobby" : "pick");
    const [showEndConfirm, setShowEndConfirm] = useState(false);

    const { state, startSession, endSession } = useLiveCompileCodingSession(
        sessionCode,
        "Profesor",
        phase === "lobby" || phase === "session",
        "host",
    );

    useEffect(() => {
        if (state.status === "running" && phase !== "session") setPhase("session");
        if (state.status === "ended") setPhase("session");
    }, [state.status, phase]);

    const handleCreate = (code: string) => {
        setSessionCode(code);
        setPhase("lobby");
        nav(`/app/coding-compile-live/host/${code}`, { replace: true });
    };

    const handleEnd = async () => {
        setShowEndConfirm(false);
        await endSession();
    };

    if (phase === "pick") {
        return <CreateSessionStep onCreate={handleCreate} />;
    }

    if (state.status === "error") {
        return (
            <div className="mx-auto max-w-lg space-y-4 py-8">
                <div className="rounded-3xl border border-red-200 bg-red-50 p-6 shadow-sm dark:border-red-900/50 dark:bg-red-900/20">
                    <div className="text-lg font-bold text-red-700 dark:text-red-300">Sesiunea compile nu este disponibilă</div>
                    <p className="mt-2 text-sm text-red-600 dark:text-red-200">{state.error}</p>
                </div>
                <button
                    onClick={() => { setPhase("pick"); setSessionCode(""); nav("/app/coding-compile-live/host"); }}
                    className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                >
                    Înapoi la configurare
                </button>
            </div>
        );
    }

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

                <button
                    onClick={() => { setPhase("pick"); setSessionCode(""); nav("/app/coding-compile-live/host"); }}
                    className="w-full rounded-2xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500"
                >
                    Lansează altă sesiune
                </button>
            </div>
        );
    }

    if (phase === "lobby" || state.status === "lobby") {
        return (
            <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
                <div className="space-y-5">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex items-center justify-between">
                            <div>
                                <div className="flex items-center gap-2">
                                    <span className="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-amber-500" />
                                    <span className="text-lg font-bold text-slate-900 dark:text-white">Lobby — Live Coding (Compile)</span>
                                </div>
                                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                                    {state.players.length} conectați • {state.tasks.length} sarcini
                                </p>
                            </div>
                            <button
                                onClick={startSession}
                                disabled={state.players.length === 0 || state.status === "connecting"}
                                className="rounded-2xl bg-emerald-600 px-6 py-3 text-sm font-bold text-white hover:bg-emerald-700 disabled:opacity-50 dark:bg-emerald-500"
                            >
                                ▶ Start Compile
                            </button>
                        </div>
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <SessionCodeDisplay code={sessionCode} />
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Sarcini configurate</div>
                        <div className="space-y-3">
                            {state.tasks.map((task: CompileCodingTask) => (
                                <div key={task.id} className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 dark:border-white/10 dark:bg-slate-950/30">
                                    <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">{task.title}</div>
                                    <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">{task.points} puncte</div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>

                <aside className="space-y-4">
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-300">Clasament live</div>
                        <LeaderboardPanel entries={state.leaderboard} compact />
                    </div>
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-5 text-sm text-slate-600 shadow-sm dark:border-white/10 dark:bg-slate-900/55 dark:text-slate-300">
                        Limbaje permise: {state.allowedLanguages.length === 0 ? "—" : state.allowedLanguages.join(", ")}
                    </div>
                </aside>
            </div>
        );
    }

    return (
        <div className="space-y-5">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                        <span className="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-red-500" />
                        <div>
                            <div className="text-sm font-bold text-slate-900 dark:text-white">{state.title || "Live Coding (Compile)"}</div>
                            <div className="text-sm text-slate-500 dark:text-slate-400">
                                {state.players.length} participanți • cod <span className="font-mono font-bold">{sessionCode}</span>
                            </div>
                        </div>
                    </div>
                    <button
                        onClick={() => setShowEndConfirm(true)}
                        className="rounded-2xl border border-red-200 bg-red-50 px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800/50 dark:bg-red-900/20 dark:text-red-300"
                    >
                        Stop
                    </button>
                </div>
            </div>

            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="mb-4 flex items-center justify-between gap-3">
                    <div>
                        <div className="text-lg font-bold text-slate-900 dark:text-white">Clasament live</div>
                        <p className="text-sm text-slate-500 dark:text-slate-400">
                            Hostul vede doar evoluția scorurilor în timpul testului compile.
                        </p>
                    </div>
                    <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                        {state.leaderboard.length} participanți clasați
                    </span>
                </div>
                <LeaderboardPanel entries={state.leaderboard} />
            </div>

            {showEndConfirm && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm">
                    <div className="w-full max-w-sm rounded-3xl border border-slate-900/10 bg-white p-6 shadow-xl dark:border-white/10 dark:bg-slate-900">
                        <div className="text-lg font-bold text-slate-900 dark:text-white">Oprești sesiunea?</div>
                        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                            Toți participanții vor vedea clasamentul final.
                        </p>
                        <div className="mt-5 flex gap-3">
                            <button
                                onClick={() => setShowEndConfirm(false)}
                                className="flex-1 rounded-2xl border border-slate-200 bg-white py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200"
                            >
                                Anulează
                            </button>
                            <button
                                onClick={handleEnd}
                                className="flex-1 rounded-2xl bg-red-600 py-2.5 text-sm font-bold text-white hover:bg-red-700"
                            >
                                Da, oprește
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
