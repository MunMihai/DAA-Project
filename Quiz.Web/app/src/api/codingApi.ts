import type { AxiosInstance } from "axios";

export type CodingRuleset = {
    name: string;
    language: string;
    notes?: string;
    rules: any[];
};

export type ValidationViolation = {
    ruleId: string;
    message: string;
};

export type ValidationResult = {
    passed: boolean;
    violations: ValidationViolation[];
};

export type LiveCodingHistoryItem = {
    sessionCode: string;
    taskName: string;
    language: string;
    status: string;
    createdAt: string;
    startedAt?: string | null;
    endedAt?: string | null;
    timeLimitSeconds: number;
    ruleCount: number;
    participantCount: number;
    submissionCount: number;
};

export type CodingViolationHistory = {
    ruleId: string;
    message: string;
};

export type LiveCodingSubmissionHistory = {
    submissionId: string;
    passed: boolean;
    pointsEarned: number;
    submittedAt: string;
    studentCode: string;
    violations: CodingViolationHistory[];
};

export type LiveCodingParticipantHistory = {
    playerId: string;
    displayName: string;
    latestScore: number;
    submissionCount: number;
    joinedAt: string;
    lastSeenAt: string;
    lastSubmittedAt?: string | null;
    submissions: LiveCodingSubmissionHistory[];
};

export type LiveCodingHistoryDetail = {
    sessionCode: string;
    taskName: string;
    language: string;
    status: string;
    createdAt: string;
    startedAt?: string | null;
    endedAt?: string | null;
    timeLimitSeconds: number;
    ruleCount: number;
    participants: LiveCodingParticipantHistory[];
};

export function codingApi(api: AxiosInstance) {
    return {
        generateRuleset: async (referenceCode: string) => {
            const { data } = await api.post<CodingRuleset>("/coding-quiz/generate-ruleset", { referenceCode });
            return data;
        },
        createSession: async (ruleset: CodingRuleset, timeLimitSeconds: number) => {
            const { data } = await api.post<{ sessionCode: string }>("/coding-sessions", { ruleset, timeLimitSeconds });
            return data;
        },
        evaluate: async (studentCode: string, ruleset: CodingRuleset) => {
            const { data } = await api.post<ValidationResult>("/coding-quiz/evaluate", { studentCode, ruleset });
            return data;
        },
        getHistory: async () => {
            const { data } = await api.get<LiveCodingHistoryItem[]>("/coding-sessions/history");
            return data;
        },
        getHistoryDetail: async (code: string) => {
            const { data } = await api.get<LiveCodingHistoryDetail>(`/coding-sessions/history/${code}`);
            return data;
        },
    };
}
