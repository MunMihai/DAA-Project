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
    };
}
