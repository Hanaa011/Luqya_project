import { useContext } from "react";
import { ConversationsContext } from "./conversationsContextDef";

export function useConversations() {
  return useContext(ConversationsContext);
}
