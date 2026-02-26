import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { type Quiz, quizApi, type QuizStatus } from "../../api/quizApi.ts";
import { useApi } from "../../api/axios.tsx";
import { toastApiError } from "../../utils/toastError.ts";

function Badge({ status }: { status: QuizStatus }) {
    const map: Record<number, { label: string; cls: string }> = {
        0: { label: "Draft", cls: "bg-slate-100 text-slate-800 dark:bg-slate-800/60 dark:text-slate-200" },
        1: { label: "Published", cls: "bg-green-100 text-green-800 dark:bg-green-500/10 dark:text-green-200" },
        2: { label: "Archived", cls: "bg-amber-100 text-amber-900 dark:bg-amber-500/10 dark:text-amber-200" },
    };
    const v = map[status] ?? map[0];
    return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${v.cls}`}>{v.label}</span>;
}

export function AdminQuizzesPage() {
    const api = useApi();
    const qapi = useMemo(() => quizApi(api), [api]);

    const [items, setItems] = useState<Quiz[]>([]);
    const [loading, setLoading] = useState(true);
    const [status, setStatus] = useState<QuizStatus | "all">("all");
    const [q, setQ] = useState("");
    const [err, setErr] = useState<string | null>(null);

    const load = async () => {
        setLoading(true);
        setErr(null);
        try {
            const data = await qapi.list(status === "all" ? undefined : { status });
            setItems(data);
        } catch (e: any) {
            setErr(e?.response?.data?.message ?? "Nu pot încărca quizurile.");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        load();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [status]);

    const filtered = useMemo(() => {
        const needle = q.trim().toLowerCase();
        if (!needle) return items;
        return items.filter((x) => (x.title ?? "").toLowerCase().includes(needle) || (x.tags ?? []).some(t => t.toLowerCase().includes(needle)));
    }, [items, q]);

    const onDelete = async (id: string) => {
        if (!confirm("Ștergi acest quiz?")) return;
        try {
            await qapi.remove(id);
            await load();
        } catch (e: any) {
            toastApiError(e?.response?.data?.message ?? "Delete failed.");
        }
    };

    return (
        <div className="space-y-6">
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
                    <div>
                        <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">Admin • Quizuri</div>
                        <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                            Creează, editează și publică quizuri (true/false, single, multiple, text scurt).
                        </div>
                    </div>

                    <div className="flex gap-3">
                        <Link
                            to="/app/admin/quizzes/new"
                            className="inline-flex items-center justify-center rounded-2xl bg-indigo-600 px-4 py-3 text-sm font-semibold text-white shadow-sm hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                        >
                            + Quiz nou
                        </Link>
                    </div>
                </div>

                <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-[220px_1fr]">
                    <select
                        value={status}
                        onChange={(e) => setStatus(e.target.value === "all" ? "all" : (Number(e.target.value) as QuizStatus))}
                        className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                    >
                        <option value="all">Toate statusurile</option>
                        <option value={0}>Draft</option>
                        <option value={1}>Published</option>
                        <option value={2}>Archived</option>
                    </select>

                    <input
                        value={q}
                        onChange={(e) => setQ(e.target.value)}
                        placeholder="Caută după titlu sau tag..."
                        className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 shadow-sm outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100"
                    />
                </div>
            </div>

            {err && (
                <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300">
                    {err}
                </div>
            )}

            <div className="rounded-3xl border border-slate-900/10 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="border-b border-slate-900/10 px-6 py-4 text-sm font-semibold text-slate-900 dark:border-white/10 dark:text-slate-100">
                    Quizuri ({filtered.length})
                </div>

                {loading ? (
                    <div className="px-6 py-10 text-sm text-slate-600 dark:text-slate-400">Loading...</div>
                ) : filtered.length === 0 ? (
                    <div className="px-6 py-10 text-sm text-slate-600 dark:text-slate-400">Niciun quiz.</div>
                ) : (
                    <div className="divide-y divide-slate-900/10 dark:divide-white/10">
                        {filtered.map((x) => (
                            <div key={x.id} className="flex flex-col gap-3 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
                                <div className="min-w-0">
                                    <div className="flex items-center gap-3">
                                        <div className="truncate text-sm font-semibold text-slate-900 dark:text-slate-100">{x.title}</div>
                                        <Badge status={x.status} />
                                    </div>
                                    <div className="mt-1 flex flex-wrap gap-2 text-xs text-slate-600 dark:text-slate-400">
                    <span className="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-950/30">
                      {x.questions?.length ?? 0} întrebări
                    </span>
                                        <span className="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-950/30">
                      {x.timeLimitSeconds}s
                    </span>
                                        {(x.tags ?? []).slice(0, 5).map((t) => (
                                            <span key={t} className="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-950/30">
                        #{t}
                      </span>
                                        ))}
                                    </div>
                                </div>

                                <div className="flex shrink-0 gap-2">
                                    <Link
                                        to={`/app/admin/quizzes/${x.id}`}
                                        className="rounded-xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-semibold text-slate-800 shadow-sm hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                                    >
                                        Edit
                                    </Link>
                                    <button
                                        onClick={() => onDelete(x.id)}
                                        className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-200 dark:hover:bg-red-900/40"
                                    >
                                        Delete
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}