import { type ReactNode, useMemo, useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { IconMoon, IconSun } from "../components/AuthShell.tsx";
import { applyTheme, getInitialTheme } from "../theme.ts";

function cn(...xs: Array<string | false | null | undefined>) {
    return xs.filter(Boolean).join(" ");
}

function IconMenu({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path d="M4 6h16M4 12h16M4 18h16" className="stroke-current" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

function IconX({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path d="M6 6l12 12M18 6 6 18" className="stroke-current" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

function IconHome({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path
                d="M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1v-9.5Z"
                className="stroke-current"
                strokeWidth="2"
                strokeLinejoin="round"
            />
        </svg>
    );
}

function IconQuiz({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path
                d="M7 4h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z"
                className="stroke-current"
                strokeWidth="2"
            />
            <path d="M8 8h8M8 12h8M8 16h5" className="stroke-current" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

function IconCode({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path d="M9 18 3 12l6-6M15 6l6 6-6 6" className="stroke-current" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

function IconTrophy({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path
                d="M8 4h8v3a4 4 0 0 1-8 0V4Z"
                className="stroke-current"
                strokeWidth="2"
                strokeLinejoin="round"
            />
            <path
                d="M8 7H5a2 2 0 0 0 2 2h1M16 7h3a2 2 0 0 1-2 2h-1"
                className="stroke-current"
                strokeWidth="2"
                strokeLinecap="round"
            />
            <path d="M12 11v4m-4 5h8" className="stroke-current" strokeWidth="2" strokeLinecap="round" />
            <path d="M10 19v-2h4v2" className="stroke-current" strokeWidth="2" strokeLinejoin="round" />
        </svg>
    );
}

function IconLogout({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden="true">
            <path
                d="M10 7V6a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-7a2 2 0 0 1-2-2v-1"
                className="stroke-current"
                strokeWidth="2"
            />
            <path d="M15 12H3m0 0 3-3M3 12l3 3" className="stroke-current" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

function NavItem({
                     to,
                     icon,
                     label,
                 }: {
    to: string;
    icon: ReactNode;
    label: string;
}) {
    return (
        <NavLink
            to={to}
            className={({ isActive }) =>
                cn(
                    "group flex items-center gap-3 rounded-2xl px-3 py-2 text-sm font-medium transition",
                    "text-slate-700 hover:bg-slate-900/5 hover:text-slate-900",
                    "dark:text-slate-200 dark:hover:bg-white/5 dark:hover:text-white",
                    isActive && "bg-indigo-600 text-white hover:bg-indigo-600 hover:text-white dark:bg-indigo-500"
                )
            }
            end
        >
      <span
          className={cn(
              "grid h-9 w-9 place-items-center rounded-xl",
              "bg-white shadow-sm ring-1 ring-slate-900/10",
              "dark:bg-slate-950/40 dark:ring-white/10",
              "group-[.active]:bg-white/20 group-[.active]:ring-white/20 group-[.active]:shadow-none"
          )}
      >
        {icon}
      </span>
            <span className="truncate">{label}</span>
        </NavLink>
    );
}

export function AppShell({ children }: { children: ReactNode }) {
    const { user, logout } = useAuth();
    const nav = useNavigate();

    const [mobileOpen, setMobileOpen] = useState(false);

    const menu = useMemo(
        () => [
            { to: "/app/admin/quizzes", label: "Admin Quizuri", icon: <IconQuiz className="h-5 w-5" /> },
            { to: "/app", label: "Dashboard", icon: <IconHome className="h-5 w-5" /> },
            { to: "/app/quizzes", label: "Quizuri", icon: <IconQuiz className="h-5 w-5" /> },
            { to: "/app/contests", label: "Concursuri", icon: <IconTrophy className="h-5 w-5" /> },
            { to: "/app/submissions", label: "Submisii", icon: <IconCode className="h-5 w-5" /> },
        ],
        []
    );

    const onLogout = () => {
        logout();
        nav("/login");
    };
    const [theme, setTheme] = useState<"light" | "dark">(getInitialTheme());
    const isDark = theme === "dark";
    const toggle = () => {
        const next = isDark ? "light" : "dark";
        setTheme(next);
        applyTheme(next);
    };
    return (
        <>
            <header className="sticky top-0 z-30 border-b border-slate-900/10 bg-white/85 backdrop-blur dark:border-white/10 dark:bg-slate-900/60">
                <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
                    <div className="flex items-center gap-3">
                        <button
                            type="button"
                            className="inline-flex items-center justify-center rounded-xl border border-slate-900/10 bg-white px-3 py-2 text-slate-800 shadow-sm hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 dark:hover:bg-slate-950/60 lg:hidden"
                            onClick={() => setMobileOpen(true)}
                            aria-label="Open menu"
                        >
                            <IconMenu className="h-5 w-5" />
                        </button>

                        <Link to="/app" className="flex items-center gap-3">
                            <div
                                className="grid h-10 w-10 place-items-center rounded-2xl bg-indigo-600 text-white shadow-sm dark:bg-indigo-500">
                                <span className="text-sm font-black tracking-tight">QA</span>
                            </div>
                            <div className="leading-tight">
                                <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                                    QuizArena
                                </div>
                                <div className="text-xs text-slate-600 dark:text-slate-400">
                                    Dashboard
                                </div>
                            </div>
                        </Link>
                    </div>

                    <div className="flex items-center gap-3">
                        <div className="hidden text-right sm:block">
                            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                                {user?.email ?? "—"}
                            </div>
                            <div className="text-xs text-slate-600 dark:text-slate-400">
                                Student
                            </div>
                        </div>

                        <button
                            onClick={onLogout}
                            className="inline-flex items-center gap-2 rounded-xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-semibold text-slate-800 shadow-sm hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 dark:hover:bg-slate-950/60"
                        >
                            <IconLogout className="h-5 w-5" />
                            <span className="hidden sm:inline">Logout</span>
                        </button>

                        <button
                            type="button"
                            onClick={toggle}
                            className="inline-flex items-center gap-2 rounded-xl border border-slate-900/10 bg-white px-3 py-2 text-sm font-medium text-slate-800 shadow-sm hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500/40 dark:border-white/10 dark:bg-slate-900/60 dark:text-slate-100 dark:hover:bg-slate-900"
                            aria-label="Toggle theme"
                        >
                            {isDark ? <IconSun className="h-4 w-4" /> : <IconMoon className="h-4 w-4" />}
                            <span className="hidden sm:inline">{isDark ? "Light" : "Dark"}</span>
                        </button>
                    </div>
                </div>
            </header>
            <div className="min-h-screen bg-slate-100 dark:bg-slate-950 ">
                {/* Header */}
                

                <div
                    className="mx-auto grid max-w-7xl grid-cols-1 gap-6 px-4 py-6 sm:px-6 lg:grid-cols-[260px_1fr] lg:px-8">
                    {/* Sidebar (desktop) */}
                    <aside className="hidden lg:block">
                        <div
                            className="sticky top-20 rounded-3xl border border-slate-900/10 bg-white p-3 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
                            <div className="px-3 py-3">
                                <div
                                    className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Navigare
                                </div>
                            </div>
                            <nav className="space-y-1">
                                {menu.map((m) => (
                                    <NavItem key={m.to} to={m.to} icon={m.icon} label={m.label} />
                                ))}
                            </nav>

                            <div
                                className="mt-4 rounded-2xl border border-slate-900/10 bg-slate-50 p-4 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300">
                                <div className="font-semibold text-slate-900 dark:text-slate-100">Tip</div>
                                <div className="mt-1 text-sm">
                                    Începe cu un quiz pentru a-ți seta nivelul, apoi intră la concursuri.
                                </div>
                            </div>
                        </div>
                    </aside>

                    {/* Mobile overlay + drawer */}
                    {mobileOpen && (
                        <div className="fixed inset-0 z-40 lg:hidden" role="dialog" aria-modal="true">
                            <div
                                className="absolute inset-0 bg-slate-900/30 backdrop-blur-sm"
                                onClick={() => setMobileOpen(false)}
                            />
                            <div
                                className="absolute inset-y-0 left-0 w-[86%] max-w-xs bg-white p-4 shadow-xl dark:bg-slate-950">
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-3">
                                        <div
                                            className="grid h-10 w-10 place-items-center rounded-2xl bg-indigo-600 text-white shadow-sm dark:bg-indigo-500">
                                            <span className="text-sm font-black tracking-tight">QA</span>
                                        </div>
                                        <div className="leading-tight">
                                            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                                                QuizArena
                                            </div>
                                            <div className="text-xs text-slate-600 dark:text-slate-400">Meniu</div>
                                        </div>
                                    </div>
                                    <button
                                        className="inline-flex items-center justify-center rounded-xl border border-slate-900/10 bg-white px-3 py-2 text-slate-800 shadow-sm hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/40 dark:text-slate-100 dark:hover:bg-slate-950/60"
                                        onClick={() => setMobileOpen(false)}
                                        aria-label="Close menu"
                                    >
                                        <IconX className="h-5 w-5" />
                                    </button>
                                </div>

                                <nav className="mt-4 space-y-1">
                                    {menu.map((m) => (
                                        <div key={m.to} onClick={() => setMobileOpen(false)}>
                                            <NavItem to={m.to} icon={m.icon} label={m.label} />
                                        </div>
                                    ))}
                                </nav>
                            </div>
                        </div>
                    )}

                    {/* Main */}
                    <main className="min-w-0">
                        {children}

                    </main>

                </div>

            </div>

            {/* Footer */}
            <footer
                className="mt-10 border-t border-slate-900/10 pt-6 text-center text-xs text-slate-600 dark:border-white/10 dark:text-slate-400">
                <div className="mx-auto max-w-3xl">
                    <div className="font-semibold text-slate-800 dark:text-slate-200">
                        QuizArena • Edu & Coding
                    </div>
                    <div className="mt-1">
                        © {new Date().getFullYear()} • Toate drepturile rezervate • Suport: support@quizarena
                    </div>
                </div>
            </footer>
        </>
    );
}