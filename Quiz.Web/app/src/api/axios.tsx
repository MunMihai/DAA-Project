import axios, { AxiosError, type AxiosInstance } from "axios";
import React, { createContext, useContext, useMemo } from "react";
import { CONFIG } from "../config.ts";
import { clearAuth, readAuth } from "../auth/tokenStore.ts";

type AxiosCtx = { api: AxiosInstance };

const AxiosContext = createContext<AxiosCtx | null>(null);

export function AxiosProvider({ children }: { children: React.ReactNode }) {
    const api = useMemo(() => {
        const instance = axios.create({
            baseURL: CONFIG.API_BASE_URL,
            withCredentials: false, // JWT in header; schimbi la true doar dacă folosești cookie auth
            timeout: 20_000,
        });

        instance.interceptors.request.use((config) => {
            const auth = readAuth();
            if (auth?.accessToken) {
                config.headers = config.headers ?? {};
                config.headers.Authorization = `Bearer ${auth.accessToken}`;
            }
            return config;
        });

        instance.interceptors.response.use(
            (res) => res,
            (err: AxiosError) => {
                // dacă primești 401, scoți tokenul și lași app-ul să redirecționeze la login
                if (err.response?.status === 401) {
                    clearAuth();
                }
                return Promise.reject(err);
            }
        );

        return instance;
    }, []);

    return <AxiosContext.Provider value={{ api }}>{children}</AxiosContext.Provider>;
}

export function useApi() {
    const ctx = useContext(AxiosContext);
    if (!ctx) throw new Error("useApi must be used within AxiosProvider");
    return ctx.api;
}