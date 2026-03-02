import { useCallback, useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { ValidationViolation } from "../api/codingApi";

// ── Types ─────────────────────────────────────────────────────────────────────

export type SessionStatus = "idle" | "connecting" | "lobby" | "running" | "ended" | "error";

export type Player = { id: string; displayName: string };

export type LeaderboardEntry = { playerId: string; displayName: string; score: number };

export type CodeAck = {
    passed: boolean;
    violations: ValidationViolation[];
    pointsEarned: number;
    yourScore: number;
};

export type LiveCodingState = {
    status: SessionStatus;
    error: string | null;
    players: Player[];
    rulesetName: string | null;
    deadlineUtc: Date | null;
    leaderboard: LeaderboardEntry[];
    lastAck: CodeAck | null;
};

// ── Hook ──────────────────────────────────────────────────────────────────────

const HUB_URL = "http://localhost:5000";

export function useLiveCodingSession(
    sessionCode: string,
    displayName: string,
    enabled: boolean,
) {
    const connRef = useRef<signalR.HubConnection | null>(null);

    const [state, setState] = useState<LiveCodingState>({
        status: "idle",
        error: null,
        players: [],
        rulesetName: null,
        deadlineUtc: null,
        leaderboard: [],
        lastAck: null,
    });

    const patch = useCallback((p: Partial<LiveCodingState>) =>
        setState(prev => ({ ...prev, ...p })), []);

    useEffect(() => {
        if (!enabled || !sessionCode || !displayName) return;

        const conn = new signalR.HubConnectionBuilder()
            .withUrl(`${HUB_URL}/coding-hubs/live-coding`)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connRef.current = conn;

        conn.on("lobbyUpdate", (d: { players: Player[] }) =>
            patch({ players: d.players }));

        conn.on("sessionStarted", (d: { rulesetName: string, deadlineUtc: string }) =>
            patch({
                status: "running",
                rulesetName: d.rulesetName,
                deadlineUtc: d.deadlineUtc ? new Date(d.deadlineUtc) : null,
                lastAck: null,
            }));

        conn.on("codeAck", (ack: CodeAck) =>
            patch({ lastAck: ack }));

        conn.on("leaderboard", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ leaderboard: d.leaderboard }));

        conn.on("sessionEnded", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ status: "ended", leaderboard: d.leaderboard }));

        conn.on("sessionState", (d: any) => {
            const isRunning = d.status === "running";
            const isEnded = d.status === "ended";

            patch({
                status: isRunning ? "running" : isEnded ? "ended" : "lobby",
                rulesetName: d.rulesetName ?? null,
                deadlineUtc: d.deadlineUtc ? new Date(d.deadlineUtc) : null,
                leaderboard: d.leaderboard ?? [],
                players: d.players ?? [],
            });
        });

        conn.on("error", (d: { message: string }) =>
            patch({ error: d.message }));

        conn.onreconnecting(() => patch({ status: "connecting", error: null }));
        conn.onreconnected(async () => {
            patch({ error: null });
            await conn.invoke("GetSessionState", sessionCode).catch(() => { });
        });
        conn.onclose(() => patch({ status: "error", error: "Conexiunea a fost închisă." }));

        const start = async () => {
            patch({ status: "connecting", error: null });
            try {
                await conn.start();
                await conn.invoke("Join", sessionCode, displayName);
                await conn.invoke("GetSessionState", sessionCode);
            } catch (err: any) {
                patch({ status: "error", error: err?.message ?? "Conexiune eșuată." });
            }
        };

        start();
        return () => { conn.stop(); };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [enabled, sessionCode, displayName]);

    // ── Actions ───────────────────────────────────────────────────────────────
    const startSession = useCallback(() =>
        connRef.current?.invoke("StartSession", sessionCode), [sessionCode]);

    const submitCode = useCallback((code: string) =>
        connRef.current?.invoke("SubmitCode", sessionCode, code),
        [sessionCode]);

    const endSession = useCallback(() =>
        connRef.current?.invoke("EndSession", sessionCode), [sessionCode]);

    return { state, startSession, submitCode, endSession };
}
