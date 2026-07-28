import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";

import App from "./App";
import { I18nProvider } from "./lib/i18n";
import { ThemeProvider } from "./lib/theme";
import { AuthProvider } from "./lib/auth";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <BrowserRouter>
      <ThemeProvider>
        <I18nProvider>
          <AuthProvider>
            <App />
          </AuthProvider>
        </I18nProvider>
      </ThemeProvider>
    </BrowserRouter>
  </React.StrictMode>
);