import { createContext } from "react";

export const AuthContext = createContext({
  profile: null,
  userId: null,
  loading: true,
  login: async () => {},
  register: async () => {},
  logout: async () => {},
  refreshProfile: async () => {},
});
