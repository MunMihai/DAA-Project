import { useEffect, type ReactNode } from "react";

export function cn(...xs: Array<string | false | null | undefined>) {
    return xs.filter(Boolean).join(" ");
}

export function formatDate(value?: string | null) {
    if (!value) return "—";
    return new Intl.DateTimeFormat("ro-RO", {
        dateStyle: "medium",
        timeStyle: "short",
    }).format(new Date(value));
}

export function formatDuration(seconds: number) {
    if (seconds <= 0) return "—";
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    if (mins === 0) return `${secs}s`;
    if (secs === 0) return `${mins} min`;
    return `${mins} min ${secs}s`;
}

export function StatusBadge({ status }: { status: string }) {
    const normalized = status.toLowerCase();
    const cls =
        normalized === "ended"
            ? "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200"
            : normalized === "running"
                ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
                : "bg-amber-100 text-amber-800 dark:bg-amber-500/10 dark:text-amber-300";

    const label = normalized === "ended" ? "Finalizat" : normalized === "running" ? "În desfășurare" : "Lobby";

    return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${cls}`}>{label}</span>;
}

export function TabButton({
    active,
    onClick,
    children,
}: {
    active: boolean;
    onClick: () => void;
    children: string;
}) {
    return (
        <button
            onClick={onClick}
            className={cn(
                "rounded-2xl px-4 py-2.5 text-sm font-semibold transition",
                active
                    ? "bg-indigo-600 text-white dark:bg-indigo-500"
                    : "border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200 dark:hover:bg-slate-950/40",
            )}
        >
            {children}
        </button>
    );
}

export function PageHero({
    title,
    description,
    actions,
}: {
    title: string;
    description: string;
    actions?: ReactNode;
}) {
    return (
        <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <div className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">{title}</div>
                    <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">{description}</div>
                </div>
                {actions && <div className="flex gap-3">{actions}</div>}
            </div>
        </div>
    );
}

export function TeacherOnlyNotice() {
    return (
        <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
            <div className="text-xl font-bold text-slate-900 dark:text-white">Istoric teste</div>
            <div className="mt-2 text-sm text-slate-600 dark:text-slate-400">
                Pagina de istoric detaliat este disponibilă pentru rolurile `Teacher` și `Admin`.
            </div>
        </div>
    );
}

export function DetailDialog({
    title,
    subtitle,
    onClose,
    children,
}: {
    title: string;
    subtitle?: string;
    onClose: () => void;
    children: ReactNode;
}) {
    useEffect(() => {
        const onKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") onClose();
        };

        window.addEventListener("keydown", onKeyDown);
        return () => window.removeEventListener("keydown", onKeyDown);
    }, [onClose]);

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm" onClick={onClose}>
            <div
                className="max-h-[90vh] w-full max-w-4xl overflow-hidden rounded-3xl border border-white/10 bg-white shadow-2xl dark:bg-slate-900"
                onClick={(event) => event.stopPropagation()}
            >
                <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-6 py-5 dark:border-white/10">
                    <div>
                        <div className="text-xl font-bold text-slate-900 dark:text-white">{title}</div>
                        {subtitle && <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">{subtitle}</div>}
                    </div>
                    <button
                        onClick={onClose}
                        className="rounded-2xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-200 dark:hover:bg-slate-950/50"
                    >
                        Închide
                    </button>
                </div>
                <div className="max-h-[calc(90vh-96px)] overflow-y-auto p-6">{children}</div>
            </div>
        </div>
    );
}
