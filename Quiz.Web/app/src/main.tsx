import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./App.tsx";
import { AxiosProvider } from "./api/axios.tsx";
import { AuthProvider } from "./auth/AuthContext.tsx";
import { ToastContainer } from "react-toastify";
import { getInitialTheme } from "./theme.ts";

ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
        <BrowserRouter>
            <AxiosProvider>
                <AuthProvider>
                    <App />
                    <ToastContainer
                        position="top-right"
                        autoClose={2500}
                        closeOnClick
                        pauseOnHover
                        newestOnTop
                        theme={getInitialTheme()}
                    />
                </AuthProvider>
            </AxiosProvider>
        </BrowserRouter>
    </React.StrictMode>
);