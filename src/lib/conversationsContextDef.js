import { createContext } from "react";

export const ConversationsContext = createContext({
  conversations: [],
  totalUnread: 0,
  incomingCall: null,
  dismissIncomingCall: () => {},
  refresh: () => {},
  loaded: false,
  loadError: null,
});
