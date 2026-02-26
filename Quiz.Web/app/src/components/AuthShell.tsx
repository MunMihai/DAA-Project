import { type ReactNode, useEffect, useState } from "react";
import { applyTheme, getInitialTheme } from "../theme.ts";

export function IconSun({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path d="M12 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12Z" className="stroke-current" strokeWidth="2" />
            <path
                d="M12 2v2m0 16v2M4 12H2m20 0h-2M5 5l1.5 1.5M17.5 17.5 19 19M19 5l-1.5 1.5M6.5 17.5 5 19"
                className="stroke-current"
                strokeWidth="2"
                strokeLinecap="round"
            />
        </svg>
    );
}

export function IconMoon({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path
                d="M21 13.2A7.6 7.6 0 0 1 10.8 3a8.6 8.6 0 1 0 10.2 10.2Z"
                className="stroke-current"
                strokeWidth="2"
                strokeLinejoin="round"
            />
        </svg>
    );
}

export function AuthShell({
                              title,
                              subtitle,
                              children,
                          }: {
    title: string;
    subtitle: string;
    children: ReactNode;
}) {
    const [theme, setTheme] = useState<"light" | "dark">(getInitialTheme());

    useEffect(() => {
        const t = getInitialTheme();
        setTheme(t);
        
        applyTheme(t);
    }, []);

    const isDark = theme === "dark";
    const toggle = () => {
        const next = isDark ? "light" : "dark";
        setTheme(next);
        applyTheme(next);
    };

    return (
        <div className="min-h-full">
            <div className="relative min-h-full">
                {/* Academic background (subtle, no flashy gradients) */}
                <div className="absolute inset-0 -z-10">
                    <div className="absolute inset-0 bg-slate-100 dark:bg-slate-950" />
                    <div className="absolute inset-x-0 top-0 h-28 bg-gradient-to-b from-white/70 to-transparent dark:from-slate-900/40" />
                    <div className="absolute inset-x-0 bottom-0 h-40 bg-gradient-to-t from-slate-200/50 to-transparent dark:from-slate-950" />
                </div>

                {/* Top bar */}
                <header className="mx-auto flex w-full max-w-6xl items-center justify-between px-4 py-6 sm:px-6 lg:px-8">
                    <div className="flex items-center gap-3">
                        <div className="grid h-10 w-10 place-items-center rounded-2xl bg-indigo-600 text-white shadow-sm dark:bg-indigo-500">
                            <span className="text-sm font-black tracking-tight">QA</span>
                        </div>
                        <div className="leading-tight">
                            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                                QuizArena
                            </div>
                            <div className="text-xs text-slate-600 dark:text-slate-400">
                                Platformă educațională pentru quizuri & coding
                            </div>
                        </div>
                    </div>

                    <button
                        type="button"
                        onClick={toggle}
                        className="inline-flex items-center gap-2 rounded-xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-medium text-slate-800 shadow-sm hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-900/60 dark:text-slate-100 dark:hover:bg-slate-900"
                        aria-label="Toggle theme"
                    >
                        {isDark ? <IconSun className="h-4 w-4" /> : <IconMoon className="h-4 w-4" />}
                        <span className="hidden sm:inline">{isDark ? "Light" : "Dark"}</span>
                    </button>
                </header>

                {/* Content */}
                <main className="mx-auto grid w-full max-w-6xl grid-cols-1 items-center gap-10 px-4 pb-12 sm:px-6 lg:grid-cols-2 lg:px-8">
                    {/* Left: academic pitch */}
                    <section className="hidden lg:block">
                        <div className="rounded-3xl border border-slate-900/10 bg-white p-8 shadow-sm dark:border-white/10 dark:bg-slate-900/50">
                            <div className="inline-flex items-center gap-2 rounded-full border border-slate-900/10 bg-slate-50 px-3 py-1 text-xs font-semibold text-slate-700 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-200">
                                Edu • Quiz • Concursuri • Live
                            </div>

                            <h1 className="mt-4 text-3xl font-extrabold tracking-tight text-slate-900 dark:text-white">
                                Testează-ți cunoștințele și concurează corect.
                            </h1>
                            <p className="mt-3 text-base leading-relaxed text-slate-700 dark:text-slate-300">
                                Quizuri pentru capitole + concursuri de programare cu evaluare automată și clasament în timp real.
                            </p>

                            <div className="mt-6 grid grid-cols-1 gap-4">
                                {[
                                    ["Chestionare structurate", "Întrebări clare, timp limitat, feedback imediat."],
                                    ["Concursuri de programare", "Submisii evaluate automat, verdict + punctaj."],
                                    ["Progres & rapoarte", "Istoric, statistici și recomandări pentru învățare."],
                                ].map(([t, d]) => (
                                    <div
                                        key={t}
                                        className="rounded-2xl border border-slate-900/10 bg-slate-50 p-4 dark:border-white/10 dark:bg-slate-950/30"
                                    >
                                        <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">{t}</div>
                                        <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">{d}</div>
                                    </div>
                                ))}
                            </div>

                            <div className="mt-6 flex items-center justify-between text-xs text-slate-600 dark:text-slate-400">
                                <span>Acces rapid pentru studenți și profesori</span>
                                <span className="font-semibold text-indigo-700 dark:text-indigo-300">Secure • Fast • Fair</span>
                            </div>
                        </div>
                    </section>

                    {/* Right: form */}
                    <section className="w-full">
                        <div className="mx-auto w-full max-w-md rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55 sm:p-8">
                            <div>
                                <h2 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
                                    {title}
                                </h2>
                                <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">{subtitle}</p>
                            </div>

                            <div className="mt-6">{children}</div>

                            <div className="mt-6 text-center text-xs text-slate-600 dark:text-slate-400">
                                Continuând, accepți{" "}
                                <span className="font-medium text-slate-800 dark:text-slate-200">
                  Termenii
                </span>{" "}
                                și{" "}
                                <span className="font-medium text-slate-800 dark:text-slate-200">
                  Politica de confidențialitate
                </span>
                                .
                            </div>
                        </div>

                        <p className="mt-6 text-center text-xs text-slate-600 dark:text-slate-400">
                            Suport: <span className="font-semibold text-slate-800 dark:text-slate-200">support@quizarena</span>
                        </p>
                    </section>
                </main>
            </div>
        </div>
    );
}