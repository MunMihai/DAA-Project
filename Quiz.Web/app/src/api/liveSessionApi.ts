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

export type LiveQuizHistoryItem = {
    sessionCode: string;
    quizId: string;
    quizTitle: string;
    status: string;
    createdAt: string;
    startedAt?: string | null;
    endedAt?: string | null;
    timeLimitSeconds: number;
    questionCount: number;
    participantCount: number;
    answerCount: number;
};

export type LiveQuizAnswerHistory = {
    questionIndex: number;
    questionId: string;
    questionType: number;
    prompt: string;
    submittedAnswer: string;
    isCorrect: boolean;
    pointsEarned: number;
    submittedAt: string;
};

export type LiveQuizParticipantHistory = {
    playerId: string;
    displayName: string;
    score: number;
    joinedAt: string;
    lastSeenAt: string;
    answerCount: number;
    answers: LiveQuizAnswerHistory[];
};

export type LiveQuizHistoryDetail = {
    sessionCode: string;
    quizId: string;
    quizTitle: string;
    status: string;
    createdAt: string;
    startedAt?: string | null;
    endedAt?: string | null;
    timeLimitSeconds: number;
    questionCount: number;
    participants: LiveQuizParticipantHistory[];
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
        getHistory: async (): Promise<LiveQuizHistoryItem[]> => {
            const { data } = await api.get<LiveQuizHistoryItem[]>("/live/live-sessions/history");
            return data;
        },
        getHistoryDetail: async (code: string): Promise<LiveQuizHistoryDetail> => {
            const { data } = await api.get<LiveQuizHistoryDetail>(`/live/live-sessions/history/${code}`);
            return data;
        },
    };
}
