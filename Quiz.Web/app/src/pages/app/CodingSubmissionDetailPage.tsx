import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useApi } from "../../api/axios.tsx";
import { codingApi, type LiveCodingHistoryDetail, type LiveCodingParticipantHistory } from "../../api/codingApi.ts";
import { useAuth } from "../../auth/AuthContext.tsx";
import {
    DetailDialog,
    PageHero,
    StatusBadge,
    TeacherOnlyNotice,
    formatDate,
    formatDuration,
} from "./history/HistoryUi.tsx";

function ParticipantDetailDialog({
    participant,
    onClose,
}: {
    participant: LiveCodingParticipantHistory;
    onClose: () => void;
}) {
    return (
        <DetailDialog
            title={participant.displayName}
            subtitle={`Scor curent ${participant.latestScore}p • ${participant.submissionCount} submit-uri`}
            onClose={onClose}
        >
            <div className="space-y-4">
                {participant.submissions.length === 0 ? (
                    <div className="rounded-2xl border border-dashed border-slate-200 px-4 py-3 text-sm text-slate-500 dark:border-white/10 dark:text-slate-400">
                        Participantul nu a trimis cod.
                    </div>
                ) : (
                    participant.submissions.map((submission) => (
                        <div key={submission.submissionId} className="rounded-2xl border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-slate-900/40">
                            <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                                <div className="text-sm text-slate-600 dark:text-slate-300">Trimis la {formatDate(submission.submittedAt)}</div>
                                <div className="flex flex-wrap gap-2 text-xs">
                                    <span className={submission.passed
                                        ? "rounded-full bg-emerald-100 px-2 py-1 font-semibold text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
                                        : "rounded-full bg-red-100 px-2 py-1 font-semibold text-red-700 dark:bg-red-500/10 dark:text-red-300"}>
                                        {submission.passed ? "Valid" : "Respins"}
                                    </span>
                                    <span className="rounded-full bg-slate-100 px-2 py-1 text-slate-600 dark:bg-slate-900/60 dark:text-slate-300">
                                        Scor: {submission.pointsEarned}p
                                    </span>
                                </div>
                            </div>

                            {submission.violations.length > 0 && (
                                <div className="mt-3 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
                                    <div className="font-semibold">Violations</div>
                                    <ul className="mt-2 space-y-1">
                                        {submission.violations.map((violation, index) => (
                                            <li key={`${submission.submissionId}-${index}`}>
                                                [{violation.ruleId}] {violation.message}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}

                            <pre className="mt-3 max-h-80 overflow-auto rounded-xl bg-slate-950 px-4 py-3 text-xs text-slate-100">
                                <code>{submission.studentCode}</code>
                            </pre>
                        </div>
                    ))
                )}
            </div>
        </DetailDialog>
    );
}

export function CodingSubmissionDetailPage() {
    const { code } = useParams<{ code: string }>();
    const api = useApi();
    const { isAdmin } = useAuth();
    const cApi = useMemo(() => codingApi(api), [api]);

    const [detail, setDetail] = useState<LiveCodingHistoryDetail | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedParticipant, setSelectedParticipant] = useState<LiveCodingParticipantHistory | null>(null);

    useEffect(() => {
        if (!isAdmin || !code) return;

        let cancelled = false;

        const load = async () => {
            setLoading(true);
            setError(null);
            try {
                const data = await cApi.getHistoryDetail(code);
                if (!cancelled) setDetail(data);
            } catch {
                if (!cancelled) {
                    setDetail(null);
                    setError("Nu pot încărca detaliile acestei sesiuni de coding.");
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        };

        load();

        return () => {
            cancelled = true;
        };
    }, [isAdmin, code, cApi]);

    if (!isAdmin) return <TeacherOnlyNotice />;

    return (
        <div className="space-y-6">
            <PageHero
                title="Detalii test coding (Rule Based)"
                description="Listă completă de participanți. Deschide popup-ul fiecărui student pentru toate submit-urile lui din sesiunea rule based."
                actions={
                    <Link
                        to="/app/submissions"
                        className="rounded-2xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200 dark:hover:bg-slate-950/40"
                    >
                        Înapoi la istoric
                    </Link>
                }
            />

            {loading ? (
                <div className="rounded-3xl border border-slate-900/10 bg-white px-6 py-8 text-sm text-slate-500 shadow-sm dark:border-white/10 dark:bg-slate-900/55 dark:text-slate-400">
                    Se încarcă detaliile sesiunii...
                </div>
            ) : error || !detail ? (
                <div className="rounded-3xl border border-red-200 bg-red-50 px-6 py-8 text-sm text-red-700 shadow-sm dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
                    {error ?? "Sesiunea nu a fost găsită."}
                </div>
            ) : (
                <>
                    <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                            <div>
                                <div className="text-2xl font-bold text-slate-900 dark:text-white">{detail.taskName}</div>
                                <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">Sesiune {detail.sessionCode}</div>
                            </div>
                            <StatusBadge status={detail.status} />
                        </div>
                        <div className="mt-4 flex flex-wrap gap-2 text-xs text-slate-500 dark:text-slate-400">
                            <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{detail.participants.length} participanți</span>
                            <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{detail.ruleCount} reguli</span>
                            <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">Durată: {formatDuration(detail.timeLimitSeconds)}</span>
                            <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">Limbaj: {detail.language || "dotnet"}</span>
                        </div>
                        <div className="mt-4 grid grid-cols-1 gap-2 text-xs text-slate-500 dark:text-slate-400 md:grid-cols-3">
                            <div>Creat: {formatDate(detail.createdAt)}</div>
                            <div>Start: {formatDate(detail.startedAt)}</div>
                            <div>Final: {formatDate(detail.endedAt)}</div>
                        </div>
                    </div>

                    <div className="rounded-3xl border border-slate-900/10 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                        <div className="border-b border-slate-900/10 px-6 py-4 text-sm font-semibold text-slate-900 dark:border-white/10 dark:text-slate-100">
                            Studenți
                        </div>
                        {detail.participants.length === 0 ? (
                            <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Niciun participant înregistrat.</div>
                        ) : (
                            <div className="divide-y divide-slate-900/10 dark:divide-white/10">
                                {detail.participants.map((participant) => (
                                    <div key={participant.playerId} className="px-6 py-5">
                                        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                                            <div className="min-w-0 flex-1">
                                                <div className="text-lg font-semibold text-slate-900 dark:text-slate-100">{participant.displayName}</div>
                                                <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                                                    Conectat: {formatDate(participant.joinedAt)} • Ultima activitate: {formatDate(participant.lastSeenAt)}
                                                </div>
                                                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                                                    <span className="rounded-full bg-indigo-50 px-2.5 py-1 font-semibold text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300">
                                                        Scor: {participant.latestScore}p
                                                    </span>
                                                    <span className="rounded-full bg-slate-100 px-2.5 py-1 text-slate-600 dark:bg-slate-900/60 dark:text-slate-300">
                                                        {participant.submissionCount} submit-uri
                                                    </span>
                                                </div>
                                            </div>
                                            <button
                                                onClick={() => setSelectedParticipant(participant)}
                                                className="rounded-2xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                                            >
                                                Vezi detalii
                                            </button>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </>
            )}

            {selectedParticipant && (
                <ParticipantDetailDialog participant={selectedParticipant} onClose={() => setSelectedParticipant(null)} />
            )}
        </div>
    );
}
