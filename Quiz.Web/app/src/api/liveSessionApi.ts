import type { AxiosInstance } from "axios";

export type CreateSessionRequest = { quizId: string };

export type CreateSessionResponse = {
    sessionCode: string;
    quizId: string;
    hubUrl: string;
    createdAt: string;
};

export type SessionInfo = {
    sessionCode: string;
    quizId: string;
    status: "lobby" | "running" | "ended";
    currentIndex: number;
    totalQuestions: number;
    playerCount: number;
    leaderboard: LeaderboardEntry[];
};

export type LeaderboardEntry = {
    playerId: string;
    displayName: string;
    score: number;
};

export function liveSessionApi(api: AxiosInstance) {
    return {
        create: async (req: CreateSessionRequest): Promise<CreateSessionResponse> => {
            const { data } = await api.post<CreateSessionResponse>("/live/live-sessions", req);
            return data;
        },
        getInfo: async (code: string): Promise<SessionInfo> => {
            const { data } = await api.get<SessionInfo>(`/live/live-sessions/${code}`);
            return data;
        },
    };
}