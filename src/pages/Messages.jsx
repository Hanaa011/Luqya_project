import { useEffect } from "react";
import { Link } from "react-router-dom";
import { MessageSquare, Loader2, AlertCircle } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useConversations } from "../lib/useConversations";
import { ReportType } from "../api/enums";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

// See Conversation.jsx's parseServerTime for why this is needed: a message
// timestamp re-fetched from the backend (as this list always does) loses
// its "Z"/UTC marker on the way through EF Core, and the ECMAScript Date
// spec then parses the unmarked string as local time instead of UTC -
// displaying it exactly the browser's UTC offset too early (3 hours behind
// in Saudi Arabia). Treat an unmarked string as UTC explicitly; the actual
// local conversion still comes from toLocaleString using the browser's own
// timezone, never a hardcoded offset.
function parseServerTime(iso) {
  const hasTimezone = /Z$|[+-]\d{2}:\d{2}$/.test(iso);
  return new Date(hasTimezone ? iso : `${iso}Z`);
}

function formatTime(iso) {
  if (!iso) return "";
  try {
    return parseServerTime(iso).toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return "";
  }
}

export default function Messages() {
  const { tr, lang } = useI18n();
  const { conversations, loaded, loadError } = useConversations();
  // Reuses the same global poll every other consumer (navbar badge,
  // incoming-call banner) already reads from - one shared fetch instead of
  // this page running its own separate one.
  const status = !loaded ? "loading" : loadError ? "error" : "success";

  useEffect(() => {
    document.title = tr({ ar: "الرسائل — لُقيا", en: "Messages — Luqya", ur: "پیغامات — لقیا" });
  }, [tr]);

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-3xl mx-auto px-6">
        <div className="text-center mb-10">
          <div className="inline-flex items-center gap-2 text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
            <MessageSquare className="size-3.5" />
            {copy(lang, { ar: "الرسائل", en: "Messages", ur: "پیغامات" })}
          </div>
          <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight mb-3">
            {copy(lang, { ar: "محادثاتك", en: "Your conversations", ur: "آپ کی بات چیت" })}
          </h1>
          <p className="text-muted-foreground text-lg max-w-xl mx-auto">
            {copy(lang, {
              ar: "تواصل مع الآخرين داخل لُقيا فقط — دون مشاركة رقم الهاتف أو البريد الإلكتروني.",
              en: "Talk to people inside Luqya only — no phone number or email is ever shared.",
              ur: "صرف لقیا کے اندر لوگوں سے بات کریں — کوئی فون نمبر یا ای میل کبھی شیئر نہیں ہوتا۔",
            })}
          </p>
        </div>

        {status === "loading" && (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="size-6 animate-spin text-muted-foreground" />
          </div>
        )}

        {status === "error" && (
          <div className="flex flex-col items-center gap-3 py-16 text-center">
            <AlertCircle className="size-6 text-error" />
            <p className="text-error text-sm">{loadError}</p>
          </div>
        )}

        {status === "success" && conversations.length === 0 && (
          <div className="text-center py-16 text-muted-foreground">
            {copy(lang, { ar: "لا توجد محادثات بعد.", en: "No conversations yet.", ur: "ابھی تک کوئی بات چیت نہیں۔" })}
          </div>
        )}

        {status === "success" && conversations.length > 0 && (
          <div className="space-y-3">
            {conversations.map((c) => {
              const lastMessage = c.messages?.[0];
              const hasUnread = (c.unreadCount || 0) > 0;
              return (
                <Link
                  key={c.id}
                  to={`/messages/${c.id}`}
                  className={`flex items-center gap-4 rounded-[1.75rem] border p-5 shadow-soft hover:shadow-luxe transition-all hover:-translate-y-0.5 ${
                    hasUnread ? "border-primary/30 bg-primary/[0.03]" : "border-border bg-card"
                  }`}
                >
                  <span className="relative grid size-12 shrink-0 place-items-center rounded-2xl bg-stone-100 text-primary font-bold font-mono">
                    {c.otherParticipantName?.slice(0, 2).toUpperCase() || "?"}
                    {hasUnread && (
                      <span className="absolute -top-1 -end-1 size-3 rounded-full bg-accent ring-2 ring-background" />
                    )}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-2">
                      <p className={`text-sm truncate ${hasUnread ? "font-extrabold" : "font-bold"}`}>
                        {c.otherParticipantName}
                      </p>
                      {lastMessage && (
                        <span className="text-xs text-muted-foreground shrink-0">
                          {formatTime(lastMessage.creationTime)}
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground truncate mt-0.5">
                      {c.reportType === ReportType.FOUND
                        ? copy(lang, { ar: "بخصوص غرض موجود", en: "About a found item", ur: "ملنے والی چیز کے بارے میں" })
                        : copy(lang, { ar: "بخصوص غرض مفقود", en: "About a lost item", ur: "کھوئی ہوئی چیز کے بارے میں" })}
                      {c.reportDescription ? ` · ${c.reportDescription}` : ""}
                    </p>
                    <div className="flex items-center justify-between gap-2 mt-1">
                      {lastMessage && (
                        <p className={`text-sm truncate ${hasUnread ? "font-semibold text-foreground" : "text-muted-foreground"}`}>
                          {lastMessage.isMine ? `${copy(lang, { ar: "أنت:", en: "You:", ur: "آپ:" })} ` : ""}
                          {lastMessage.text}
                        </p>
                      )}
                      {hasUnread && (
                        <span className="shrink-0 inline-flex min-w-5 h-5 items-center justify-center rounded-full bg-accent px-1.5 text-[11px] font-bold leading-none text-white">
                          {c.unreadCount > 9 ? "9+" : c.unreadCount}
                        </span>
                      )}
                    </div>
                  </div>
                </Link>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}
