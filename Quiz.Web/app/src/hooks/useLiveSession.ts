import { useCallback, useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";

// ── Types ─────────────────────────────────────────────────────────────────────

export type SessionStatus = "idle" | "connecting" | "lobby" | "running" | "ended" | "error";

export type Player = { id: string; displayName: string };

export type LeaderboardEntry = { playerId: string; displayName: string; score: number };

export type QuizOption = { id: string; text: string };

export type QuizQuestion = {
    id: string;
    type: 0 | 1 | 2 | 3; // TrueFalse | Single | Multiple | ShortText
    prompt: string;
    points: number;
    options: QuizOption[];
};

export type AnswerPayload = {
    boolAnswer?: boolean | null;
    singleOptionId?: string | null;
    multipleOptionIds?: string[] | null;
    textAnswer?: string | null;
};

export type AnswerAck = {
    isCorrect: boolean;
    pointsEarned: number;
    alreadyAnswered: boolean;
    expired: boolean;
    yourScore: number;
};

export type LiveState = {
    status: SessionStatus;
    error: string | null;
    players: Player[];
    currentQuestion: QuizQuestion | null;
    questionIndex: number;
    totalQuestions: number;
    deadlineUtc: Date | null;
    timeLimitSeconds: number;
    leaderboard: LeaderboardEntry[];
    lastAck: AnswerAck | null;
    playerFinished: boolean;
};

// ── Hook ──────────────────────────────────────────────────────────────────────

const HUB_URL = "http://localhost:5000";

export function useLiveSession(
    sessionCode: string,
    displayName: string,
    enabled: boolean,
) {
    const connRef = useRef<signalR.HubConnection | null>(null);

    const [state, setState] = useState<LiveState>({
        status: "idle",
        error: null,
        players: [],
        currentQuestion: null,
        questionIndex: -1,
        totalQuestions: 0,
        deadlineUtc: null,
        timeLimitSeconds: 30,
        leaderboard: [],
        lastAck: null,
        playerFinished: false,
    });

    const patch = useCallback((p: Partial<LiveState>) =>
        setState(prev => ({ ...prev, ...p })), []);

    useEffect(() => {
        if (!enabled || !sessionCode || !displayName) return;

        const conn = new signalR.HubConnectionBuilder()
            .withUrl(`${HUB_URL}/hubs/live-quiz`)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connRef.current = conn;

        conn.on("lobbyUpdate", (d: { players: Player[] }) =>
            patch({ players: d.players }));

        conn.on("sessionStarted", (d: { totalQuestions: number, deadlineUtc?: string }) =>
            patch({
                status: "running",
                totalQuestions: d.totalQuestions,
                lastAck: null,
                playerFinished: false,
                deadlineUtc: d.deadlineUtc ? new Date(d.deadlineUtc) : null
            }));

        conn.on("questionStarted", (d: {
            index: number;
            question: QuizQuestion;
            deadlineUtc: string;
            timeLimitSeconds: number;
        }) => patch({
            questionIndex: d.index,
            currentQuestion: d.question,
            deadlineUtc: new Date(d.deadlineUtc),
            timeLimitSeconds: d.timeLimitSeconds,
            lastAck: null,
            status: "running",
        }));

        conn.on("questionEnded", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ leaderboard: d.leaderboard }));

        conn.on("playerFinished", () =>
            patch({ playerFinished: true }));

        conn.on("answerAck", (ack: AnswerAck) =>
            patch({ lastAck: ack }));

        conn.on("leaderboard", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ leaderboard: d.leaderboard }));

        conn.on("sessionEnded", (d: { leaderboard: LeaderboardEntry[] }) =>
            patch({ status: "ended", leaderboard: d.leaderboard, currentQuestion: null }));

        conn.on("sessionState", (d: any) => {
            const isRunning = d.status === "running";
            const isEnded = d.status === "ended";

            patch({
                status: isRunning ? "running" : isEnded ? "ended" : "lobby",
                totalQuestions: d.totalQuestions ?? 0,
                questionIndex: d.currentIndex ?? -1,
                currentQuestion: d.currentQuestion ?? null,
                deadlineUtc: d.deadlineUtc ? new Date(d.deadlineUtc) : null,
                leaderboard: d.leaderboard ?? [],
                players: d.players ?? [],
                playerFinished: !!d.playerFinished,
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

    const fetchNextQuestion = useCallback(() =>
        connRef.current?.invoke("FetchNextQuestion", sessionCode), [sessionCode]);

    const submitAnswer = useCallback((payload: AnswerPayload) =>
        connRef.current?.invoke("SubmitAnswer", sessionCode, state.questionIndex, payload),
        [sessionCode, state.questionIndex]);

    const endSession = useCallback(() =>
        connRef.current?.invoke("EndSession", sessionCode), [sessionCode]);

    return { state, startSession, fetchNextQuestion, submitAnswer, endSession };
}