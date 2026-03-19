import { useCallback, useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { ValidationViolation } from "../api/codingApi";
import { extractErrorMessage, toastErrorMessage } from "../utils/toastError";

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

const HUB_URL = "/coding-hubs/live-coding";
const SESSION_CODE_REGEX = /^[A-Z0-9]{6}$/;
const MAX_DISPLAY_NAME_LENGTH = 50;

const initialState: LiveCodingState = {
    status: "idle",
    error: null,
    players: [],
    rulesetName: null,
    deadlineUtc: null,
    leaderboard: [],
    lastAck: null,
};

export function useLiveCodingSession(
    sessionCode: string,
    displayName: string,
    enabled: boolean,
    mode: "player" | "host" = "player",
) {
    const connRef = useRef<signalR.HubConnection | null>(null);
    const stateRef = useRef<LiveCodingState>(initialState);
    const suppressCloseErrorRef = useRef(false);

    const [state, setState] = useState<LiveCodingState>(initialState);

    useEffect(() => {
        stateRef.current = state;
    }, [state]);

    const patch = useCallback((p: Partial<LiveCodingState>) =>
        setState(prev => ({ ...prev, ...p })), []);

    const pushError = useCallback((message: string, fatal = false) => {
        const normalizedMessage = message.trim() || "A apărut o eroare.";
        if (fatal && stateRef.current.status === "error" && stateRef.current.error === normalizedMessage) {
            return;
        }

        setState(prev => ({
            ...prev,
            error: normalizedMessage,
            status: fatal ? "error" : prev.status,
        }));
        toastErrorMessage(normalizedMessage, `live-coding:${mode}:${sessionCode}:${normalizedMessage}`);
    }, [mode, sessionCode]);

    const reportError = useCallback((err: any, fallback: string, fatal = false) => {
        const message = extractErrorMessage(err, fallback);
        pushError(message, fatal);
        return message;
    }, [pushError]);

    useEffect(() => {
        if (!enabled) {
            connRef.current = null;
            setState(initialState);
            return;
        }

        const codeError = validateSessionCode(sessionCode);
        if (codeError) {
            pushError(codeError, true);
            return;
        }

        const displayNameError = validateDisplayName(displayName);
        if (displayNameError) {
            pushError(displayNameError, true);
            return;
        }

        setState({ ...initialState, status: "connecting" });

        const conn = new signalR.HubConnectionBuilder()
            .withUrl(HUB_URL)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connRef.current = conn;
        suppressCloseErrorRef.current = false;

        conn.on("lobbyUpdate", (d: { players: Player[] }) =>
            patch({ players: d.players ?? [] }));

        conn.on("sessionStarted", (d: { rulesetName: string; deadlineUtc?: string }) =>
            patch({
                status: "running",
                rulesetName: d.rulesetName ?? "Live Coding (Rule Based)",
                deadlineUtc: d.deadlineUtc ? new Date(d.deadlineUtc) : null,
                lastAck: null,
            }));

        conn.on("codeAck", (ack: CodeAck) =>
            patch({ lastAck: ack }));

        conn.on("leaderboard", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ leaderboard: d.leaderboard ?? [] }));

        conn.on("sessionEnded", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ status: "ended", leaderboard: d.leaderboard ?? [] }));

        conn.on("sessionState", (d: any) => {
            const isRunning = d.status === "running";
            const isEnded = d.status === "ended";

            patch({
                status: isRunning ? "running" : isEnded ? "ended" : "lobby",
                rulesetName: d.rulesetName ?? null,
                deadlineUtc: d.deadlineUtc ? new Date(d.deadlineUtc) : null,
                leaderboard: d.leaderboard ?? [],
                players: d.players ?? [],
                error: null,
            });
        });

        conn.on("error", (d: { message?: string }) => {
            const message = typeof d?.message === "string" && d.message.trim()
                ? d.message.trim()
                : "A apărut o eroare în sesiunea live.";
            const fatal = conn.state !== signalR.HubConnectionState.Connected
                || stateRef.current.status === "connecting"
                || stateRef.current.status === "idle";
            pushError(message, fatal);
        });

        conn.onreconnecting(() => patch({ status: "connecting", error: null }));

        const joinCurrent = async () => {
            if (mode === "host") {
                await conn.invoke("JoinHost", sessionCode, displayName);
                return;
            }

            await conn.invoke("Join", sessionCode, displayName);
        };

        let disposed = false;

        conn.onreconnected(async () => {
            patch({ error: null });
            suppressCloseErrorRef.current = false;
            try {
                await joinCurrent();
                await conn.invoke("GetSessionState", sessionCode);
            } catch (err: any) {
                if (!disposed) {
                    reportError(err, "Nu mă pot reconecta la sesiunea live de coding.", true);
                }
            }
        });

        conn.onclose((err) => {
            if (disposed) {
                return;
            }

            if (suppressCloseErrorRef.current) {
                suppressCloseErrorRef.current = false;
                return;
            }

            const message = err
                ? extractErrorMessage(err, "Conexiunea a fost închisă.")
                : "Conexiunea a fost închisă.";
            pushError(message, true);
        });

        const start = async () => {
            try {
                await conn.start();
                await joinCurrent();
                await conn.invoke("GetSessionState", sessionCode);
            } catch (err: any) {
                suppressCloseErrorRef.current = true;
                await conn.stop().catch(() => undefined);
                reportError(err, "Conexiunea la sesiunea de coding a eșuat.", true);
            }
        };

        start();

        return () => {
            disposed = true;
            connRef.current = null;
            conn.stop().catch(() => undefined);
        };
    }, [enabled, sessionCode, displayName, mode, patch, pushError, reportError]);

    const startSession = useCallback(async () => {
        if (!connRef.current) {
            pushError("Conexiunea live nu este disponibilă.");
            return false;
        }

        try {
            await connRef.current.invoke("StartSession", sessionCode);
            return true;
        } catch (err: any) {
            reportError(err, "Nu pot porni sesiunea.");
            return false;
        }
    }, [sessionCode, pushError, reportError]);

    const submitCode = useCallback(async (code: string) => {
        if (!connRef.current) {
            pushError("Conexiunea live nu este disponibilă.");
            return false;
        }

        if (!code.trim()) {
            pushError("Codul sursă nu poate fi gol.");
            return false;
        }

        try {
            await connRef.current.invoke("SubmitCode", sessionCode, code);
            return true;
        } catch (err: any) {
            reportError(err, "Codul nu a putut fi trimis.");
            return false;
        }
    }, [sessionCode, pushError, reportError]);

    const endSession = useCallback(async () => {
        if (!connRef.current) {
            pushError("Conexiunea live nu este disponibilă.");
            return false;
        }

        try {
            await connRef.current.invoke("EndSession", sessionCode);
            return true;
        } catch (err: any) {
            reportError(err, "Nu pot închide sesiunea.");
            return false;
        }
    }, [sessionCode, pushError, reportError]);

    return { state, startSession, submitCode, endSession };
}

function validateSessionCode(sessionCode: string) {
    if (!sessionCode.trim()) {
        return "Codul sesiunii este obligatoriu.";
    }

    if (!SESSION_CODE_REGEX.test(sessionCode.trim().toUpperCase())) {
        return "Codul sesiunii trebuie să aibă exact 6 caractere alfanumerice.";
    }

    return null;
}

function validateDisplayName(displayName: string) {
    const normalized = displayName.trim();
    if (!normalized) {
        return "Numele afișat este obligatoriu.";
    }

    if (normalized.length > MAX_DISPLAY_NAME_LENGTH) {
        return `Numele afișat poate avea cel mult ${MAX_DISPLAY_NAME_LENGTH} de caractere.`;
    }

    return null;
}
