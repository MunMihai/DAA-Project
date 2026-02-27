import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import {
    clearAuth,
    extractRoles,
    isExpired,
    readAuth,
    writeAuth,
    type StoredAuth,
} from "./tokenStore";
import { authApi } from "../api/authApi.ts";
import { useApi } from "../api/axios.tsx";

type AuthUser = { email: string; roles: string[] };

type AuthState = {
    user: AuthUser | null;
    token: string | null;
    isReady: boolean;
    isAdmin: boolean;
    login: (email: string, password: string) => Promise<void>;
    signup: (email: string, password: string) => Promise<void>;
    logout: () => void;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
    const api = useApi();
    const apiAuth = useMemo(() => authApi(api), [api]);

    const [isReady, setIsReady] = useState(false);
    const [user, setUser] = useState<AuthUser | null>(null);
    const [token, setToken] = useState<string | null>(null);

    useEffect(() => {
        const stored = readAuth();
        if (stored && !isExpired(stored.expiresAt)) {
            setUser({ email: stored.email, roles: stored.roles ?? [] });
            setToken(stored.accessToken);
        } else {
            clearAuth();
        }
        setIsReady(true);
    }, []);

    const apply = (auth: StoredAuth) => {
        const roles = extractRoles(auth.accessToken);
        const full: StoredAuth = { ...auth, roles };
        writeAuth(full);
        setUser({ email: full.email, roles });
        setToken(full.accessToken);
    };

    const login = async (email: string, password: string) => {
        const res = await apiAuth.login({ email, password });
        apply({ accessToken: res.accessToken, expiresAt: res.expiresAt, email: res.email, roles: [] });
    };

    const signup = async (email: string, password: string) => {
        const res = await apiAuth.signup({ email, password });
        apply({ accessToken: res.accessToken, expiresAt: res.expiresAt, email: res.email, roles: [] });
    };

    const logout = () => {
        clearAuth();
        setUser(null);
        setToken(null);
    };

    const isAdmin = user?.roles.some(r =>
        r.toLowerCase() === "admin" || r.toLowerCase() === "teacher"
    ) ?? false;

    const value: AuthState = { user, token, isReady, isAdmin, login, signup, logout };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error("useAuth must be used within AuthProvider");
    return ctx;
}