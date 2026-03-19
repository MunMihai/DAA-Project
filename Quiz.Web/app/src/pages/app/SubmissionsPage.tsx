import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { useApi } from "../../api/axios.tsx";
import { codingApi, type LiveCodingHistoryItem } from "../../api/codingApi.ts";
import { compileCodingApi, type CompileCodingHistoryItem } from "../../api/compileCodingApi.ts";
import { liveSessionApi, type LiveQuizHistoryItem } from "../../api/liveSessionApi.ts";
import { useAuth } from "../../auth/AuthContext.tsx";
import { PageHero, StatusBadge, TabButton, TeacherOnlyNotice } from "./history/HistoryUi.tsx";

function SessionListSection({
    title,
    children,
}: {
    title: string;
    children: ReactNode;
}) {
    return (
        <div className="rounded-3xl border border-slate-900/10 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/55">
            <div className="border-b border-slate-900/10 px-6 py-4 text-sm font-semibold text-slate-900 dark:border-white/10 dark:text-slate-100">
                {title}
            </div>
            {children}
        </div>
    );
}

function QuizSessionRow({ item }: { item: LiveQuizHistoryItem }) {
    return (
        <div className="px-6 py-5">
            <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <div className="truncate text-lg font-semibold text-slate-900 dark:text-slate-100">{item.quizTitle}</div>
                        <StatusBadge status={item.status} />
                    </div>
                    <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">Cod sesiune: {item.sessionCode}</div>
                    <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-500 dark:text-slate-400">
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.participantCount} participanți</span>
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.answerCount} răspunsuri</span>
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.questionCount} întrebări</span>
                    </div>
                </div>

                <div className="flex flex-col items-start gap-3 xl:items-end">
                    <div className="text-xs text-slate-500 dark:text-slate-400">Creat: {new Date(item.createdAt).toLocaleString("ro-RO")}</div>
                    <Link
                        to={`/app/submissions/quiz/${item.sessionCode}`}
                        className="rounded-2xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                    >
                        Deschide detalii
                    </Link>
                </div>
            </div>
        </div>
    );
}

function CodingSessionRow({ item }: { item: LiveCodingHistoryItem }) {
    return (
        <div className="px-6 py-5">
            <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <div className="truncate text-lg font-semibold text-slate-900 dark:text-slate-100">{item.taskName}</div>
                        <StatusBadge status={item.status} />
                    </div>
                    <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">Cod sesiune: {item.sessionCode}</div>
                    <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-500 dark:text-slate-400">
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.participantCount} participanți</span>
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.submissionCount} submit-uri</span>
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.ruleCount} reguli</span>
                    </div>
                </div>

                <div className="flex flex-col items-start gap-3 xl:items-end">
                    <div className="text-xs text-slate-500 dark:text-slate-400">Creat: {new Date(item.createdAt).toLocaleString("ro-RO")}</div>
                    <Link
                        to={`/app/submissions/coding/${item.sessionCode}`}
                        className="rounded-2xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                    >
                        Deschide detalii
                    </Link>
                </div>
            </div>
        </div>
    );
}

function CompileCodingSessionRow({ item }: { item: CompileCodingHistoryItem }) {
    return (
        <div className="px-6 py-5">
            <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <div className="truncate text-lg font-semibold text-slate-900 dark:text-slate-100">{item.title}</div>
                        <StatusBadge status={item.status} />
                    </div>
                    <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">Cod sesiune: {item.sessionCode}</div>
                    <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-500 dark:text-slate-400">
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.participantCount} participanți</span>
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.submissionCount} submit-uri</span>
                        <span className="rounded-full bg-slate-100 px-2.5 py-1 dark:bg-slate-900/60">{item.taskCount} sarcini</span>
                    </div>
                </div>

                <div className="flex flex-col items-start gap-3 xl:items-end">
                    <div className="text-xs text-slate-500 dark:text-slate-400">Creat: {new Date(item.createdAt).toLocaleString("ro-RO")}</div>
                    <Link
                        to={`/app/submissions/coding-compile/${item.sessionCode}`}
                        className="rounded-2xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 dark:bg-indigo-500 dark:hover:bg-indigo-400"
                    >
                        Deschide detalii
                    </Link>
                </div>
            </div>
        </div>
    );
}

export function SubmissionsPage() {
    const api = useApi();
    const { isAdmin } = useAuth();
    const liveApi = useMemo(() => liveSessionApi(api), [api]);
    const cApi = useMemo(() => codingApi(api), [api]);
    const compileApi = useMemo(() => compileCodingApi(api), [api]);

    const [tab, setTab] = useState<"quiz" | "coding" | "compile">("quiz");
    const [quizItems, setQuizItems] = useState<LiveQuizHistoryItem[]>([]);
    const [codingItems, setCodingItems] = useState<LiveCodingHistoryItem[]>([]);
    const [compileItems, setCompileItems] = useState<CompileCodingHistoryItem[]>([]);
    const [quizLoading, setQuizLoading] = useState(true);
    const [codingLoading, setCodingLoading] = useState(true);
    const [compileLoading, setCompileLoading] = useState(true);
    const [quizError, setQuizError] = useState<string | null>(null);
    const [codingError, setCodingError] = useState<string | null>(null);
    const [compileError, setCompileError] = useState<string | null>(null);

    useEffect(() => {
        if (!isAdmin) return;

        let cancelled = false;

        const load = async () => {
            setQuizLoading(true);
            setCodingLoading(true);
            setCompileLoading(true);
            setQuizError(null);
            setCodingError(null);
            setCompileError(null);

            const [quizRes, codingRes, compileRes] = await Promise.allSettled([
                liveApi.getHistory(),
                cApi.getHistory(),
                compileApi.getHistory(),
            ]);
            if (cancelled) return;

            if (quizRes.status === "fulfilled") setQuizItems(quizRes.value);
            else setQuizError("Nu pot încărca istoricul sesiunilor de quiz.");

            if (codingRes.status === "fulfilled") setCodingItems(codingRes.value);
            else setCodingError("Nu pot încărca istoricul sesiunilor de coding rule based.");

            if (compileRes.status === "fulfilled") setCompileItems(compileRes.value);
            else setCompileError("Nu pot încărca istoricul sesiunilor de coding compile.");

            setQuizLoading(false);
            setCodingLoading(false);
            setCompileLoading(false);
        };

        load();

        return () => {
            cancelled = true;
        };
    }, [isAdmin, liveApi, cApi, compileApi]);

    if (!isAdmin) {
        return <TeacherOnlyNotice />;
    }

    const renderQuizList = () => {
        if (quizLoading) return <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Se încarcă sesiunile de quiz...</div>;
        if (quizError) return <div className="px-6 py-8 text-sm text-red-600 dark:text-red-300">{quizError}</div>;
        if (quizItems.length === 0) return <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Nu există sesiuni de quiz salvate.</div>;

        return (
            <div className="divide-y divide-slate-900/10 dark:divide-white/10">
                {quizItems.map((item) => <QuizSessionRow key={item.sessionCode} item={item} />)}
            </div>
        );
    };

    const renderCodingList = () => {
        if (codingLoading) return <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Se încarcă sesiunile de coding rule based...</div>;
        if (codingError) return <div className="px-6 py-8 text-sm text-red-600 dark:text-red-300">{codingError}</div>;
        if (codingItems.length === 0) return <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Nu există sesiuni de coding rule based salvate.</div>;

        return (
            <div className="divide-y divide-slate-900/10 dark:divide-white/10">
                {codingItems.map((item) => <CodingSessionRow key={item.sessionCode} item={item} />)}
            </div>
        );
    };

    const renderCompileList = () => {
        if (compileLoading) return <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Se încarcă sesiunile de coding compile...</div>;
        if (compileError) return <div className="px-6 py-8 text-sm text-red-600 dark:text-red-300">{compileError}</div>;
        if (compileItems.length === 0) return <div className="px-6 py-8 text-sm text-slate-500 dark:text-slate-400">Nu există sesiuni de coding compile salvate.</div>;

        return (
            <div className="divide-y divide-slate-900/10 dark:divide-white/10">
                {compileItems.map((item) => <CompileCodingSessionRow key={item.sessionCode} item={item} />)}
            </div>
        );
    };

    const sectionTitle =
        tab === "quiz"
            ? "Sesiuni quiz"
            : tab === "coding"
                ? "Sesiuni coding (Rule Based)"
                : "Sesiuni coding (Compile)";

    return (
        <div className="space-y-6">
            <PageHero
                title="Istoric teste"
                description="Vezi toate sesiunile de quiz, coding rule based și coding compile în format listă, apoi deschide pagina dedicată a fiecărui test."
                actions={
                    <>
                        <TabButton active={tab === "quiz"} onClick={() => setTab("quiz")}>Quiz</TabButton>
                        <TabButton active={tab === "coding"} onClick={() => setTab("coding")}>Coding (Rule Based)</TabButton>
                        <TabButton active={tab === "compile"} onClick={() => setTab("compile")}>Coding (Compile)</TabButton>
                    </>
                }
            />

            <SessionListSection title={sectionTitle}>
                {tab === "quiz" ? renderQuizList() : tab === "coding" ? renderCodingList() : renderCompileList()}
            </SessionListSection>
        </div>
    );
}
