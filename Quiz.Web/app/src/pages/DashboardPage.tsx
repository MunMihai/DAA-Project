import { Link } from "react-router-dom";

function Card({ title, children }: { title: string; children: React.ReactNode }) {
    return (
        <div className="rounded-3xl border border-slate-900/10 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">{title}</div>
            <div className="mt-3">{children}</div>
        </div>
    );
}

function Kpi({ label, value, hint }: { label: string; value: string; hint: string }) {
    return (
        <div className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 dark:border-white/10 dark:bg-slate-950/30">
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                {label}
            </div>
            <div className="mt-2 text-2xl font-extrabold text-slate-900 dark:text-white">{value}</div>
            <div className="mt-1 text-xs text-slate-600 dark:text-slate-400">{hint}</div>
        </div>
    );
}

export function DashboardPage() {
    return (
        <div className="space-y-6">
            {/* Page title */}
            <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                    Dashboard
                </div>
                <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                    Imagine de ansamblu asupra activității tale: quizuri, concursuri și progres.
                </div>
            </div>

            {/* KPI row */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <Kpi label="Quizuri completate" value="12" hint="Ultimele 30 zile" />
                <Kpi label="Scor mediu" value="78%" hint="Stabil" />
                <Kpi label="Concursuri active" value="2" hint="Live acum" />
                <Kpi label="Submisii" value="9" hint="Săptămâna aceasta" />
            </div>

            {/* Quick actions + activity */}
            <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                <Card title="Acțiuni rapide">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                        <Link
                            to="/app/quizzes"
                            className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 text-sm font-semibold text-slate-900 hover:bg-slate-100 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                        >
                            Începe un quiz
                            <div className="mt-1 text-xs font-normal text-slate-600 dark:text-slate-400">
                                Alege capitolul și timpul.
                            </div>
                        </Link>

                        <Link
                            to="/app/contests"
                            className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 text-sm font-semibold text-slate-900 hover:bg-slate-100 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                        >
                            Vezi concursuri
                            <div className="mt-1 text-xs font-normal text-slate-600 dark:text-slate-400">
                                Intră în competiția live.
                            </div>
                        </Link>

                        <Link
                            to="/app/submissions"
                            className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 text-sm font-semibold text-slate-900 hover:bg-slate-100 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-100 dark:hover:bg-slate-950/40"
                        >
                            Istoric submisii
                            <div className="mt-1 text-xs font-normal text-slate-600 dark:text-slate-400">
                                Verdict + punctaj.
                            </div>
                        </Link>

                        <div className="rounded-2xl border border-indigo-200 bg-indigo-50 p-4 text-sm font-semibold text-indigo-900 dark:border-indigo-500/30 dark:bg-indigo-500/10 dark:text-indigo-200">
                            Recomandare
                            <div className="mt-1 text-xs font-normal text-indigo-800/80 dark:text-indigo-200/80">
                                Fă un quiz “Networking Basics” azi.
                            </div>
                        </div>
                    </div>
                </Card>

                <Card title="Activitate recentă">
                    <div className="overflow-hidden rounded-2xl border border-slate-900/10 dark:border-white/10">
                        <div className="grid grid-cols-[1fr_auto] gap-3 bg-slate-50 px-4 py-3 text-xs font-semibold text-slate-600 dark:bg-slate-950/30 dark:text-slate-400">
                            <div>Eveniment</div>
                            <div>Data</div>
                        </div>
                        {[
                            ["Quiz: Linux basics (84%)", "astăzi"],
                            ["Submission: A + B (Accepted)", "ieri"],
                            ["Contest: Algo Sprint – înscris", "acum 2 zile"],
                            ["Quiz: OOP (72%)", "acum 4 zile"],
                        ].map(([event, when]) => (
                            <div
                                key={event}
                                className="grid grid-cols-[1fr_auto] gap-3 border-t border-slate-900/10 px-4 py-3 text-sm text-slate-800 dark:border-white/10 dark:text-slate-200"
                            >
                                <div className="truncate">{event}</div>
                                <div className="text-slate-500 dark:text-slate-400">{when}</div>
                            </div>
                        ))}
                    </div>
                </Card>
            </div>
        </div>
    );
}