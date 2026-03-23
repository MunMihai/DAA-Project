import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext.tsx";

function SectionCard({
    title,
    description,
    children,
}: {
    title: string;
    description?: string;
    children: React.ReactNode;
}) {
    return (
        <div className="rounded-3xl border border-slate-900/10 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/55">
            <div className="text-lg font-bold text-slate-900 dark:text-slate-100">{title}</div>
            {description && (
                <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">{description}</div>
            )}
            <div className="mt-5">{children}</div>
        </div>
    );
}

function ActionTile({
    to,
    title,
    description,
}: {
    to: string;
    title: string;
    description: string;
}) {
    return (
        <Link
            to={to}
            className="rounded-2xl border border-slate-200 bg-slate-50 p-4 transition hover:bg-slate-100 dark:border-white/10 dark:bg-slate-950/30 dark:hover:bg-slate-950/45"
        >
            <div className="text-sm font-semibold text-slate-900 dark:text-slate-100">{title}</div>
            <div className="mt-1 text-sm text-slate-600 dark:text-slate-400">{description}</div>
        </Link>
    );
}

export function DashboardPage() {
    const { user, isAdmin } = useAuth();

    const primaryActions = isAdmin
        ? [
            {
                to: "/app/admin/quizzes",
                title: "Administrare quizuri",
                description: "Creează, editează și publică quizurile folosite în platformă.",
            },
            {
                to: "/app/live/host",
                title: "Live Quiz",
                description: "Pornește o sesiune live pentru quizurile clasice cu clasament în timp real.",
            },
            {
                to: "/app/coding-live/host",
                title: "Live Coding (Rule Based)",
                description: "Lansează o sesiune de coding evaluată după ruleset-uri generate sau definite manual.",
            },
            {
                to: "/app/coding-compile-live/host",
                title: "Live Coding (Compile)",
                description: "Creează sesiuni compile cu sarcini input/output și reutilizează șabloane salvate.",
            },
            {
                to: "/app/submissions",
                title: "Istoric teste",
                description: "Vezi sesiunile trecute, participanții și răspunsurile sau submisile lor.",
            },
        ]
        : [
            {
                to: "/app/quizzes",
                title: "Quizuri",
                description: "Accesează quizurile publicate și rezolvă testele individuale.",
            },
            {
                to: "/app/live",
                title: "Intră în Live Quiz",
                description: "Conectează-te la o sesiune live folosind codul primit de la profesor.",
            },
            {
                to: "/app/coding-live",
                title: "Live Coding (Rule Based)",
                description: "Participă la sesiuni de coding evaluate structural pe baza regulilor definite.",
            },
            {
                to: "/app/coding-compile-live",
                title: "Live Coding (Compile)",
                description: "Rezolvă sarcini de compilare cu input/output și feedback pe cazuri de test.",
            },
            {
                to: "/app/submissions",
                title: "Submisii",
                description: "Revino la încercările tale și verifică rezultatele obținute.",
            },
        ];

    const modules = isAdmin
        ? [
            "Management quizuri și publicare",
            "Sesiuni live pentru quizuri clasice",
            "Sesiuni live pentru coding rule-based",
            "Sesiuni live pentru coding compile",
            "Istoric complet de sesiuni și participanți",
        ]
        : [
            "Quizuri individuale cu cronometru",
            "Participare la sesiuni live pe cod de acces",
            "Evaluare coding rule-based",
            "Evaluare coding compile cu feedback pe cazuri",
            "Istoric pentru răspunsuri și submisii",
        ];

    return (
        <div className="space-y-6">
            <SectionCard
                title={isAdmin ? "Panou profesor" : "Dashboard student"}
                description={`Cont activ: ${user?.email ?? "—"}`}
            >
                <div className="rounded-2xl bg-slate-50 px-4 py-4 text-sm leading-6 text-slate-700 dark:bg-slate-950/35 dark:text-slate-300">
                    {isAdmin
                        ? "Aici ai doar punctele de intrare reale ale aplicației: creare quizuri, pornire sesiuni live și acces la istoric."
                        : "Dashboardul a fost simplificat să îți arate doar modulele active și acțiunile pe care le poți folosi imediat."}
                </div>
            </SectionCard>

            <SectionCard
                title="Acțiuni disponibile"
                description="Toate cardurile de mai jos duc către fluxuri funcționale, fără date demonstrative sau secțiuni artificiale."
            >
                <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
                    {primaryActions.map((action) => (
                        <ActionTile
                            key={action.to}
                            to={action.to}
                            title={action.title}
                            description={action.description}
                        />
                    ))}
                </div>
            </SectionCard>

            <SectionCard
                title="Module active în platformă"
                description="Lista reflectă capabilitățile disponibile în interfața curentă."
            >
                <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
                    {modules.map((item) => (
                        <div
                            key={item}
                            className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700 dark:border-white/10 dark:bg-slate-950/30 dark:text-slate-300"
                        >
                            {item}
                        </div>
                    ))}
                </div>
            </SectionCard>
        </div>
    );
}
