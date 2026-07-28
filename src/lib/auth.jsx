import { useEffect, useState, useCallback } from "react";
import * as authApi from "../api/auth";
import { onUnauthorized, ApiError } from "../api/httpClient";
import { getToken } from "../api/tokenStore";
import { getUserIdFromToken } from "./jwt";
import { AuthContext } from "./authContext";

export function AuthProvider({ children }) {
  const [profile, setProfile] = useState(null);
  const [userId, setUserId] = useState(null);
  const [loading, setLoading] = useState(true);

  const refreshProfile = useCallback(async () => {
    try {
      const data = await authApi.getMyProfile();
      setProfile(data);
      setUserId(getUserIdFromToken(getToken()));
      return data;
    } catch (err) {
      if (err instanceof ApiError && err.isUnauthorized) {
        setProfile(null);
        setUserId(null);
        return null;
      }
      throw err;
    }
  }, []);

  useEffect(() => {
    // Bearer-token session: only ask the backend for a profile if a token
    // was actually persisted from a previous login — a guest who never
    // logged in shouldn't fire an API call (and a guaranteed 401) on
    // every page load.
    let cancelled = false;

    Promise.resolve().then(() => {
      if (cancelled) return;

      if (getToken()) {
        refreshProfile().finally(() => !cancelled && setLoading(false));
      } else {
        setLoading(false);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [refreshProfile]);

  useEffect(() => {
    return onUnauthorized(() => {
      setProfile(null);
      setUserId(null);
    });
  }, []);

  async function login(credentials) {
    await authApi.login(credentials);
    return refreshProfile();
  }

  async function register(payload) {
    return authApi.register(payload);
  }

  async function logout() {
    await authApi.logout();
    setProfile(null);
    setUserId(null);
  }

  return (
    <AuthContext.Provider value={{ profile, userId, loading, login, register, logout, refreshProfile }}>
      {children}
    </AuthContext.Provider>
  );
}
