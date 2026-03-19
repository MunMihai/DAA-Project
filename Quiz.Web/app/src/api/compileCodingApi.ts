import type { AxiosInstance } from "axios";

export type CompileCodingTask = {
    id: string;
    title: string;
    problemStatement: string;
    inputDescription: string;
    outputDescription: string;
    exampleInput: string;
    exampleOutput: string;
    points: number;
};

export type CompileCodingTemplateCase = {
    input: string;
    expectedOutput: string;
    isExample: boolean;
};

export type CompileCodingExampleSolution = {
    language: string;
    sourceCode: string;
    notes: string;
};

export type CompileCodingTemplateTask = CompileCodingTask & {
    testCases: CompileCodingTemplateCase[];
    exampleSolutions: CompileCodingExampleSolution[];
};

export type CompileCodingTemplate = {
    id: string;
    slug: string;
    fingerprint: string;
    title: string;
    description: string;
    suggestedTimeLimitSeconds: number;
    allowedLanguages: string[];
    tasks: CompileCodingTemplateTask[];
    createdAt: string;
    updatedAt: string;
};

export type CreateCompileCodingTaskRequest = Omit<CompileCodingTask, "id"> & {
    id?: string | null;
};

export type CreateCompileCodingSessionRequest = {
    title: string;
    timeLimitSeconds: number;
    allowedLanguages: string[];
    tasks: CreateCompileCodingTaskRequest[];
};

export type CreateCompileCodingSessionResponse = {
    sessionCode: string;
    hubUrl: string;
    createdAt: string;
};

export type CompileCodingHistoryItem = {
    sessionCode: string;
    title: string;
    status: string;
    createdAt: string;
    startedAt?: string | null;
    endedAt?: string | null;
    timeLimitSeconds: number;
    taskCount: number;
    allowedLanguages: string[];
    participantCount: number;
    submissionCount: number;
};

export type CompileCodingCaseResult = {
    input: string;
    expectedOutput: string;
    actualOutput: string;
    passed: boolean;
    isExample: boolean;
    errorMessage?: string | null;
};

export type CompileCodingSubmissionHistory = {
    submissionId: string;
    taskId: string;
    taskTitle: string;
    language: string;
    passed: boolean;
    scoreDelta: number;
    bestTaskScore: number;
    totalScore: number;
    passedCaseCount: number;
    totalCaseCount: number;
    compileError?: string | null;
    runtimeError?: string | null;
    submittedAt: string;
    studentCode: string;
    cases: CompileCodingCaseResult[];
};

export type CompileCodingParticipantHistory = {
    playerId: string;
    displayName: string;
    latestScore: number;
    submissionCount: number;
    joinedAt: string;
    lastSeenAt: string;
    lastSubmittedAt?: string | null;
    submissions: CompileCodingSubmissionHistory[];
};

export type CompileCodingHistoryDetail = {
    sessionCode: string;
    title: string;
    status: string;
    createdAt: string;
    startedAt?: string | null;
    endedAt?: string | null;
    timeLimitSeconds: number;
    taskCount: number;
    allowedLanguages: string[];
    participants: CompileCodingParticipantHistory[];
};

export function compileCodingApi(api: AxiosInstance) {
    return {
        createSession: async (req: CreateCompileCodingSessionRequest) => {
            const { data } = await api.post<CreateCompileCodingSessionResponse>("/compile-coding-sessions", req);
            return data;
        },
        getTemplates: async () => {
            const { data } = await api.get<CompileCodingTemplate[]>("/compile-coding-templates");
            return data;
        },
        getHistory: async () => {
            const { data } = await api.get<CompileCodingHistoryItem[]>("/compile-coding-sessions/history");
            return data;
        },
        getHistoryDetail: async (code: string) => {
            const { data } = await api.get<CompileCodingHistoryDetail>(`/compile-coding-sessions/history/${code}`);
            return data;
        },
    };
}
