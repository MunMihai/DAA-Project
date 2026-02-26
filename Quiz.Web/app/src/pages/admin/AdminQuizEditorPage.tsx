import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
    quizApi,
    type Quiz,
    type QuizCreateRequest,
    type QuizStatus,
    type QuestionType,
    type QuestionUpsert,
} from "../../api/quizApi.ts"
import { useApi } from "../../api/axios.tsx";
import { toast } from "react-toastify";

function uid() {
    return Math.random().toString(16).slice(2) + Date.now().toString(16);
}

function Input(props: React.InputHTMLAttributes<HTMLInputElement>) {
    return (
        <input
            {...props}
            className={
                "w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 " +
                "dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 " +
                (props.className ?? "")
            }
        />
    );
}

function Textarea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
    return (
        <textarea
            {...props}
            className={
                "w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 " +
                "dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 " +
                (props.className ?? "")
            }
        />
    );
}

function Select(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
    return (
        <select
            {...props}
            className={
                "w-full rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 " +
                "dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 " +
                (props.className ?? "")
            }
        />
    );
}

const typeLabels: Record<number, string> = {
    0: "True / False",
    1: "Single choice",
    2: "Multiple choice",
    3: "Text scurt",
};

function emptyQuestion(type: QuestionType): QuestionUpsert {
    return {
        id: uid(),
        type,
        prompt: "",
        explanation: "",
        points: 1,
        options: type === 1 || type === 2 ? [{ id: uid(), text: "Option A" }, { id: uid(), text: "Option B" }] : [],
        correctBool: type === 0 ? true : null,
        correctOptionIds: [],
        acceptedAnswers: [],
        topic: "",
    };
}

function TagsInput({ value, onChange }: { value: string[]; onChange: (v: string[]) => void }) {
    const [text, setText] = useState("");

    const add = () => {
        const t = text.trim();
        if (!t) return;
        const next = Array.from(new Set([...value, t]));
        onChange(next);
        setText("");
    };

    return (
        <div>
            <div className="flex gap-2">
                <Input value={text} onChange={(e) => setText(e.target.value)} placeholder="tag (ex: Linux)" />
                <button
                    type="button"
                    onClick={add}
                    className="rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white hover:bg-slate-800 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100"
                >
                    Add
                </button>
            </div>
            <div className="mt-2 flex flex-wrap gap-2">
                {value.map((t) => (
                    <button
                        key={t}
                        type="button"
                        onClick={() => onChange(value.filter((x) => x !== t))}
                        className="rounded-full border border-slate-900/10 bg-white px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200 dark:hover:bg-slate-950/40"
                        title="Remove"
                    >
                        #{t} ✕
                    </button>
                ))}
            </div>
        </div>
    );
}

function OptionsEditor({
                           q,
                           onChange,
                       }: {
    q: QuestionUpsert;
    onChange: (q: QuestionUpsert) => void;
}) {
    const addOpt = () => {
        const id = uid();
        onChange({ ...q, options: [...q.options, { id, text: `Option ${q.options.length + 1}` }] });
    };

    const setOptText = (id: string, text: string) => {
        onChange({ ...q, options: q.options.map((o) => (o.id === id ? { ...o, text } : o)) });
    };

    const removeOpt = (id: string) => {
        onChange({
            ...q,
            options: q.options.filter((o) => o.id !== id),
            correctOptionIds: (q.correctOptionIds ?? []).filter((x) => x !== id),
        });
    };

    const toggleCorrect = (id: string) => {
        if (q.type === 1) {
            onChange({ ...q, correctOptionIds: [id] });
        } else {
            const cur = new Set(q.correctOptionIds ?? []);
            if (cur.has(id)) cur.delete(id);
            else cur.add(id);
            onChange({ ...q, correctOptionIds: Array.from(cur) });
        }
    };

    return (
        <div className="space-y-2">
            {q.options.map((o) => {
                const checked = (q.correctOptionIds ?? []).includes(o.id ?? "");
                return (
                    <div key={o.id} className="flex items-center gap-2">
                        <button
                            type="button"
                            onClick={() => toggleCorrect(o.id!)}
                            className={
                                "h-10 w-10 rounded-2xl border text-sm font-bold " +
                                (checked
                                    ? "border-indigo-300 bg-indigo-50 text-indigo-700 dark:border-indigo-500/30 dark:bg-indigo-500/10 dark:text-indigo-200"
                                    : "border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300 dark:hover:bg-slate-950/40")
                            }
                            title="Mark correct"
                        >
                            ✓
                        </button>

                        <Input value={o.text} onChange={(e) => setOptText(o.id!, e.target.value)} />

                        <button
                            type="button"
                            onClick={() => removeOpt(o.id!)}
                            className="rounded-2xl border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-200 dark:hover:bg-red-900/40"
                        >
                            Remove
                        </button>
                    </div>
                );
            })}

            <button
                type="button"
                onClick={addOpt}
                className="rounded-2xl border border-slate-900/10 bg-white px-4 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
            >
                + Add option
            </button>

            <div className="text-xs text-slate-600 dark:text-slate-400">
                {q.type === 1 ? "Single choice: selectează exact 1 corect." : "Multiple choice: poți selecta mai multe corecte."}
            </div>
        </div>
    );
}

function ShortTextAnswersEditor({
                                    q,
                                    onChange,
                                }: {
    q: QuestionUpsert;
    onChange: (q: QuestionUpsert) => void;
}) {
    const [text, setText] = useState("");

    const add = () => {
        const t = text.trim();
        if (!t) return;
        const next = Array.from(new Set([...(q.acceptedAnswers ?? []), t]));
        onChange({ ...q, acceptedAnswers: next });
        setText("");
    };

    return (
        <div>
            <div className="flex gap-2">
                <Input value={text} onChange={(e) => setText(e.target.value)} placeholder="Răspuns acceptat (ex: tcp)" />
                <button
                    type="button"
                    onClick={add}
                    className="rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white hover:bg-slate-800 dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100"
                >
                    Add
                </button>
            </div>

            <div className="mt-2 flex flex-wrap gap-2">
                {(q.acceptedAnswers ?? []).map((a) => (
                    <button
                        key={a}
                        type="button"
                        onClick={() => onChange({ ...q, acceptedAnswers: (q.acceptedAnswers ?? []).filter((x) => x !== a) })}
                        className="rounded-full border border-slate-900/10 bg-white px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200 dark:hover:bg-slate-950/40"
                    >
                        {a} ✕
                    </button>
                ))}
            </div>

            <div className="mt-2 text-xs text-slate-600 dark:text-slate-400">
                Matching este “exact” după normalizare (trim + lower). Pune variante/sinonime dacă ai.
            </div>
        </div>
    );
}

export function AdminQuizEditorPage() {
    const api = useApi();
    const qapi = useMemo(() => quizApi(api), [api]);

    const { id } = useParams<{ id: string }>();
    const isNew = id === "new";

    const nav = useNavigate();

    const [loading, setLoading] = useState(!isNew);
    const [saving, setSaving] = useState(false);
    const [err, setErr] = useState<string | null>(null);

    const [status, setStatus] = useState<QuizStatus>(0);
    const [title, setTitle] = useState("");
    const [description, setDescription] = useState("");
    const [tags, setTags] = useState<string[]>([]);
    const [timeLimitSeconds, setTimeLimitSeconds] = useState(600);
    const [shuffleQuestions, setShuffleQuestions] = useState(true);
    const [shuffleOptions, setShuffleOptions] = useState(true);
    const [questions, setQuestions] = useState<QuestionUpsert[]>([]);

    useEffect(() => {
        if (isNew) {
            setQuestions([emptyQuestion(0), emptyQuestion(1)]);
            setLoading(false);
            return;
        }

        (async () => {
            setLoading(true);
            setErr(null);
            try {
                const quiz: Quiz = await qapi.getById(id!);
                setStatus(quiz.status);
                setTitle(quiz.title ?? "");
                setDescription(quiz.description ?? "");
                setTags(quiz.tags ?? []);
                setTimeLimitSeconds(quiz.timeLimitSeconds ?? 600);
                setShuffleQuestions(!!quiz.shuffleQuestions);
                setShuffleOptions(!!quiz.shuffleOptions);

                // map questions from server (already similar)
                const qs = (quiz.questions ?? []).map((q: any) => ({
                    id: q.id,
                    type: q.type,
                    prompt: q.prompt ?? "",
                    explanation: q.explanation ?? "",
                    points: q.points ?? 1,
                    options: (q.options ?? []).map((o: any) => ({ id: o.id, text: o.text })),
                    correctBool: q.correctBool ?? null,
                    correctOptionIds: q.correctOptionIds ?? [],
                    acceptedAnswers: q.acceptedAnswers ?? [],
                    topic: q.topic ?? "",
                })) as QuestionUpsert[];

                setQuestions(qs);
            } catch (e: any) {
                setErr(e?.response?.data?.message ?? "Nu pot încărca quiz-ul.");
            } finally {
                setLoading(false);
            }
        })();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [id]);

    const updateQ = (qid: string, next: QuestionUpsert) => {
        setQuestions((prev) => prev.map((x) => (x.id === qid ? next : x)));
    };

    const addQuestion = (type: QuestionType) => setQuestions((prev) => [...prev, emptyQuestion(type)]);
    const removeQuestion = (qid: string) => setQuestions((prev) => prev.filter((x) => x.id !== qid));

    const validate = (): string | null => {
        if (!title.trim()) return "Title este obligatoriu.";
        if (questions.length === 0) return "Adaugă cel puțin o întrebare.";
        for (const q of questions) {
            if (!q.prompt.trim()) return "Toate întrebările trebuie să aibă prompt.";
            if (q.points <= 0) return "Points trebuie > 0.";
            if (q.type === 1 || q.type === 2) {
                if ((q.options ?? []).length < 2) return "Întrebările cu opțiuni trebuie să aibă minim 2 opțiuni.";
                if ((q.correctOptionIds ?? []).length === 0) return "Selectează răspunsul corect (pentru single/multiple choice).";
                if (q.type === 1 && (q.correctOptionIds ?? []).length !== 1) return "Single choice trebuie să aibă exact 1 opțiune corectă.";
            }
            if (q.type === 0 && q.correctBool === null) return "True/False trebuie să aibă correctBool setat.";
            if (q.type === 3 && (q.acceptedAnswers ?? []).length === 0) return "Text scurt trebuie să aibă minim 1 răspuns acceptat.";
        }
        return null;
    };

    const onSave = async () => {
        const v = validate();
        if (v) {
            setErr(v);
            return;
        }

        setSaving(true);
        setErr(null);

        // NOTE: backend-ul tău așteaptă numeric enums
        const base: QuizCreateRequest = {
            title: title.trim(),
            description: description.trim(),
            tags,
            timeLimitSeconds: Number(timeLimitSeconds) || 600,
            shuffleQuestions,
            shuffleOptions,
            questions: questions.map((q) => ({
                id: q.id,
                type: q.type,
                prompt: q.prompt,
                explanation: q.explanation ?? null,
                points: Number(q.points) || 1,
                options: (q.options ?? []).map((o) => ({ id: o.id, text: o.text })),
                correctBool: q.type === 0 ? !!q.correctBool : null,
                correctOptionIds: q.type === 1 || q.type === 2 ? (q.correctOptionIds ?? []) : [],
                acceptedAnswers: q.type === 3 ? (q.acceptedAnswers ?? []) : [],
                topic: q.topic ?? null,
            })),
        };

        try {
            if (isNew) {
                const created = await qapi.create(base);
                nav(`/app/admin/quizzes/${created.id}`);
            } else {
                await qapi.update(id!, { ...base, status });
                toast.success("Saved.");
            }
        } catch (e: any) {
            setErr(e?.response?.data?.message ?? "Save failed.");
        } finally {
            setSaving(false);
        }
    };

    const onPublish = async () => {
        if (isNew) return;
        try {
            await qapi.update(id!, {
                title: title.trim(),
                description: description.trim(),
                tags,
                timeLimitSeconds: Number(timeLimitSeconds) || 600,
                shuffleQuestions,
                shuffleOptions,
                questions,
                status: 1,
            } as any);
            setStatus(1);
            toast.success("Published.");
        } catch (e: any) {
            setErr(e?.response?.data?.message ?? "Publish failed.");
        }
    };

    return (
        <div className="space-y-6">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                        <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                            {isNew ? "Admin • Quiz nou" : "Admin • Edit quiz"}
                        </div>
                        <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                            Configurează metadata și întrebările. Pentru “play”, quiz-ul trebuie să fie Published.
                        </div>
                    </div>

                    <div className="flex flex-wrap gap-2">
                        {!isNew && (() => {
                            const badge = {
                                0: { label: "Draft",     cls: "bg-amber-100 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300" },
                                1: { label: "Published", cls: "bg-green-100 text-green-700 dark:bg-green-500/10 dark:text-green-300" },
                                2: { label: "Archived",  cls: "bg-slate-100 text-slate-500 dark:bg-slate-950/30 dark:text-slate-400" },
                            }[status];

                            return (
                                <span className={`rounded-2xl px-4 py-3 text-sm font-semibold ${badge?.cls}`}>
                                    {badge?.label}
                                </span>
                            );
                        })()}
                        {!isNew && status !== 1 && (
                            <button
                                type="button"
                                onClick={onPublish}
                                className="rounded-2xl bg-green-600 px-4 py-3 text-sm font-semibold text-white hover:bg-green-700 dark:bg-green-500 dark:hover:bg-green-400"
                            >
                                Publish
                            </button>
                        )}
                        <button
                            type="button"
                            onClick={onSave}
                            disabled={saving || loading}
                            className="rounded-2xl bg-indigo-600 px-4 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                        >
                            {saving ? "Saving..." : "Save"}
                        </button>
                    </div>
                </div>
            </div>

            {err && (
                <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                    {err}
                </div>
            )}

            {loading ? (
                <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                    <div className="text-sm text-slate-600 dark:text-slate-400">Loading...</div>
                </div>
            ) : (
                <>
                    {/* Metadata */}
                    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                        <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">Metadata</div>

                            <div className="mt-4 space-y-3">
                                <div>
                                    <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Title</div>
                                    <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Ex: Linux Basics" />
                                </div>

                                <div>
                                    <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Description</div>
                                    <Textarea
                                        value={description}
                                        onChange={(e) => setDescription(e.target.value)}
                                        rows={4}
                                        placeholder="Descriere scurtă..."
                                    />
                                </div>

                                <div>
                                    <div className="mb-2 text-sm font-medium text-slate-800 dark:text-slate-200">Tags</div>
                                    <TagsInput value={tags} onChange={setTags} />
                                </div>
                            </div>
                        </div>

                        <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">Settings</div>

                            <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
                                <div>
                                    <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Time limit (sec)</div>
                                    <Input
                                        type="number"
                                        min={30}
                                        value={timeLimitSeconds}
                                        onChange={(e) => setTimeLimitSeconds(Number(e.target.value))}
                                    />
                                </div>

                                {!isNew && (
                                    <div>
                                        <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Status</div>
                                        <Select value={status} onChange={(e) => setStatus(Number(e.target.value) as QuizStatus)}>
                                            <option value={0}>Draft</option>
                                            <option value={1}>Published</option>
                                            <option value={2}>Archived</option>
                                        </Select>
                                    </div>
                                )}

                                <label className="flex items-center gap-2 rounded-2xl border border-slate-900/10 bg-slate-50 p-3 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300">
                                    <input
                                        type="checkbox"
                                        checked={shuffleQuestions}
                                        onChange={(e) => setShuffleQuestions(e.target.checked)}
                                        className="h-4 w-4 rounded border-slate-300 bg-slate-50 text-indigo-600 focus:ring-indigo-500/40 dark:border-white/20 dark:bg-slate-950/40"
                                    />
                                    Shuffle questions
                                </label>

                                <label className="flex items-center gap-2 rounded-2xl border border-slate-900/10 bg-slate-50 p-3 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300">
                                    <input
                                        type="checkbox"
                                        checked={shuffleOptions}
                                        onChange={(e) => setShuffleOptions(e.target.checked)}
                                        className="h-4 w-4 rounded border-slate-300 bg-slate-50 text-indigo-600 focus:ring-indigo-500/40 dark:border-white/20 dark:bg-slate-950/40"
                                    />
                                    Shuffle options
                                </label>
                            </div>
                        </div>
                    </div>

                    {/* Questions */}
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                            <div>
                                <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">Întrebări</div>
                                <div className="mt-1 text-xs text-slate-600 dark:text-slate-400">
                                    Configurează răspunsurile corecte (nu vor fi trimise în /play).
                                </div>
                            </div>

                            <div className="flex flex-wrap gap-2">
                                <button
                                    type="button"
                                    onClick={() => addQuestion(0)}
                                    className="rounded-2xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                                >
                                    + True/False
                                </button>
                                <button
                                    type="button"
                                    onClick={() => addQuestion(1)}
                                    className="rounded-2xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                                >
                                    + Single
                                </button>
                                <button
                                    type="button"
                                    onClick={() => addQuestion(2)}
                                    className="rounded-2xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                                >
                                    + Multiple
                                </button>
                                <button
                                    type="button"
                                    onClick={() => addQuestion(3)}
                                    className="rounded-2xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                                >
                                    + Text
                                </button>
                            </div>
                        </div>

                        <div className="mt-5 space-y-4">
                            {questions.map((q, idx) => (
                                <div
                                    key={q.id}
                                    className="rounded-3xl border border-slate-900/10 bg-slate-50 p-4 dark:border-white/10 dark:bg-slate-950/30"
                                >
                                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                                        <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                                            Q{idx + 1} • {typeLabels[q.type]}
                                        </div>
                                        <button
                                            type="button"
                                            onClick={() => removeQuestion(q.id!)}
                                            className="rounded-2xl border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-200 dark:hover:bg-red-900/40"
                                        >
                                            Remove
                                        </button>
                                    </div>

                                    <div className="mt-3 grid grid-cols-1 gap-3 lg:grid-cols-4">
                                        <div className="lg:col-span-3">
                                            <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Prompt</div>
                                            <Textarea
                                                value={q.prompt}
                                                onChange={(e) => updateQ(q.id!, { ...q, prompt: e.target.value })}
                                                rows={3}
                                                placeholder="Întrebarea..."
                                            />
                                        </div>
                                        <div>
                                            <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Points</div>
                                            <Input
                                                type="number"
                                                min={1}
                                                value={q.points}
                                                onChange={(e) => updateQ(q.id!, { ...q, points: Number(e.target.value) })}
                                            />

                                            <div className="mt-3 mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Topic</div>
                                            <Input
                                                value={q.topic ?? ""}
                                                onChange={(e) => updateQ(q.id!, { ...q, topic: e.target.value })}
                                                placeholder="ex: networking"
                                            />
                                        </div>
                                    </div>

                                    <div className="mt-3">
                                        <div className="mb-1 text-sm font-medium text-slate-800 dark:text-slate-200">Explanation (optional)</div>
                                        <Textarea
                                            value={q.explanation ?? ""}
                                            onChange={(e) => updateQ(q.id!, { ...q, explanation: e.target.value })}
                                            rows={2}
                                            placeholder="Apare după submit (de ce e corect)."
                                        />
                                    </div>

                                    <div className="mt-4">
                                        {q.type === 0 && (
                                            <div className="flex flex-wrap items-center gap-3">
                                                <div className="text-sm font-medium text-slate-800 dark:text-slate-200">Correct:</div>
                                                <button
                                                    type="button"
                                                    onClick={() => updateQ(q.id!, { ...q, correctBool: true })}
                                                    className={
                                                        "rounded-2xl px-4 py-2 text-sm font-semibold " +
                                                        (q.correctBool === true
                                                            ? "bg-indigo-600 text-white dark:bg-indigo-500"
                                                            : "border border-slate-900/10 bg-white text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40")
                                                    }
                                                >
                                                    True
                                                </button>
                                                <button
                                                    type="button"
                                                    onClick={() => updateQ(q.id!, { ...q, correctBool: false })}
                                                    className={
                                                        "rounded-2xl px-4 py-2 text-sm font-semibold " +
                                                        (q.correctBool === false
                                                            ? "bg-indigo-600 text-white dark:bg-indigo-500"
                                                            : "border border-slate-900/10 bg-white text-slate-800 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40")
                                                    }
                                                >
                                                    False
                                                </button>
                                            </div>
                                        )}

                                        {(q.type === 1 || q.type === 2) && (
                                            <OptionsEditor q={q} onChange={(next) => updateQ(q.id!, next)} />
                                        )}

                                        {q.type === 3 && (
                                            <ShortTextAnswersEditor q={q} onChange={(next) => updateQ(q.id!, next)} />
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </>
            )}
        </div>
    );
}