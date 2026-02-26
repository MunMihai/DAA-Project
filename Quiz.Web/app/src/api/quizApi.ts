import type { AxiosInstance } from "axios";

export type QuizStatus = 0 | 1 | 2; // Draft=0, Published=1, Archived=2
export type QuestionType = 0 | 1 | 2 | 3; // TrueFalse, SingleChoice, MultipleChoice, ShortText

export type OptionUpsert = { id?: string | null; text: string };
export type QuestionUpsert = {
    id?: string | null;
    type: QuestionType;
    prompt: string;
    explanation?: string | null;
    points: number;
    options: OptionUpsert[];
    correctBool?: boolean | null;
    correctOptionIds: string[];
    acceptedAnswers: string[];
    topic?: string | null;
};

export type QuizCreateRequest = {
    title: string;
    description: string;
    tags: string[];
    timeLimitSeconds: number;
    shuffleQuestions: boolean;
    shuffleOptions: boolean;
    questions: QuestionUpsert[];
};

export type QuizUpdateRequest = QuizCreateRequest & { status: QuizStatus };

export type Quiz = {
    id: string;
    title: string;
    description: string;
    status: QuizStatus;
    tags: string[];
    timeLimitSeconds: number;
    shuffleQuestions: boolean;
    shuffleOptions: boolean;
    questions: any[];
    createdAt: string;
    updatedAt: string;
};

export function quizApi(api: AxiosInstance) {
    return {
        list: async (params?: { status?: QuizStatus; tag?: string }) => {
            const { data } = await api.get<Quiz[]>("/quiz/quizzes", { params });
            return data;
        },
        getById: async (id: string) => {
            const { data } = await api.get<Quiz>(`/quiz/quizzes/${id}`);
            return data;
        },
        create: async (req: QuizCreateRequest) => {
            const { data } = await api.post<Quiz>("/quiz/quizzes", req);
            return data;
        },
        update: async (id: string, req: QuizUpdateRequest) => {
            await api.put(`/quiz/quizzes/${id}`, req);
        },
        remove: async (id: string) => {
            await api.delete(`/quiz/quizzes/${id}`);
        },
    };
}