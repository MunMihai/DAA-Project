import type { AxiosInstance } from "axios";

export type SignupRequest = { email: string; password: string };
export type LoginRequest = { email: string; password: string };

export type AuthResponse = {
    accessToken: string;
    expiresAt: string; // ISO
    email: string;
};

export function authApi(api: AxiosInstance) {
    return {
        signup: async (req: SignupRequest) => {
            const { data } = await api.post<AuthResponse>("/identity/auth/signup", req);
            return data;
        },
        login: async (req: LoginRequest) => {
            const { data } = await api.post<AuthResponse>("/identity/auth/login", req);
            return data;
        },
    };
}