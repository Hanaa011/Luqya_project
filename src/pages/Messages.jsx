import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  MessageSquare,
  Loader2,
  AlertCircle,
  Search,
  Inbox,
  ShieldCheck,
  MessageCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useConversations } from "../lib/useConversations";
import { ReportType } from "../api/enums";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

function parseServerTime(iso) {
  const hasTimezone = /Z$|[+-]\d{2}:\d{2}$/.test(iso);
  return new Date(hasTimezone ? iso : `${iso}Z`);
}

function localeFor(lang) {
  if (lang === "ar") return "ar-SA";
  if (lang === "ur") return "ur-PK";
  return "en-US";
}

function formatTime(iso, lang) {
  if (!iso) return "";

  try {
    const date = parseServerTime(iso);
    const now = new Date();

    const sameDay =
      date.getFullYear() === now.getFullYear() &&
      date.getMonth() === now.getMonth() &&
      date.getDate() === now.getDate();

    const yesterday = new Date(now);
    yesterday.setDate(now.getDate() - 1);

    const isYesterday =
      date.getFullYear() === yesterday.getFullYear() &&
      date.getMonth() === yesterday.getMonth() &&
      date.getDate() === yesterday.getDate();

    if (sameDay) {
      return date.toLocaleTimeString(localeFor(lang), {
        hour: "2-digit",
        minute: "2-digit",
      });
    }

    if (isYesterday) {
      return copy(lang, {
        ar: "أمس",
        en: "Yesterday",
        ur: "کل",
      });
    }

    return date.toLocaleDateString(localeFor(lang), {
      month: "short",
      day: "numeric",
    });
  } catch {
    return "";
  }
}

function getInitials(name) {
  const clean = String(name ?? "").trim();

  if (!clean) return "?";

  const parts = clean.split(/\s+/).filter(Boolean);

  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }

  return `${parts[0][0] ?? ""}${parts[1][0] ?? ""}`.toUpperCase();
}

export default function Messages() {
  const { tr, lang, dir } = useI18n();
  const { conversations, loaded, loadError } = useConversations();

  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState("all");

  const status = !loaded ? "loading" : loadError ? "error" : "success";

  useEffect(() => {
    document.title = tr({
      ar: "الرسائل — لُقيا",
      en: "Messages — Luqya",
      ur: "پیغامات — لقیا",
    });
  }, [tr]);

  const unreadTotal = useMemo(
    () =>
      conversations.reduce(
        (total, conversation) => total + (conversation.unreadCount || 0),
        0
      ),
    [conversations]
  );

  const filteredConversations = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();

    return conversations.filter((conversation) => {
      const unread = (conversation.unreadCount || 0) > 0;

      if (filter === "unread" && !unread) {
        return false;
      }

      if (!normalizedQuery) {
        return true;
      }

      const searchableText = [
        conversation.otherParticipantName,
        conversation.reportDescription,
        conversation.messages?.[0]?.text,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      return searchableText.includes(normalizedQuery);
    });
  }, [conversations, filter, query]);

  return (
    <section
      dir={dir}
      className="min-h-[calc(100vh-80px)] bg-background py-8 sm:py-10 lg:py-12"
    >
      <div className="mx-auto max-w-5xl px-4 sm:px-6">
        {/* Header */}
        <header className="mb-7 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <div className="mb-2 inline-flex items-center gap-2 text-xs font-bold text-primary">
              <MessageSquare className="size-4" strokeWidth={1.8} />
              {copy(lang, {
                ar: "الرسائل",
                en: "Messages",
                ur: "پیغامات",
              })}
            </div>

            <h1 className="font-display text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl">
              {copy(lang, {
                ar: "محادثاتك",
                en: "Your conversations",
                ur: "آپ کی بات چیت",
              })}
            </h1>

            <p className="mt-2 max-w-xl text-sm leading-6 text-muted-foreground">
              {copy(lang, {
                ar: "تواصل بأمان داخل لُقيا دون مشاركة بياناتك الشخصية.",
                en: "Communicate securely inside Luqya without sharing personal contact details.",
                ur: "اپنی ذاتی معلومات شیئر کیے بغیر لقیا کے اندر محفوظ طریقے سے رابطہ کریں۔",
              })}
            </p>
          </div>

          <div className="inline-flex w-fit items-center gap-2 rounded-full bg-primary/[0.055] px-3 py-2 text-xs font-semibold text-primary">
            <ShieldCheck className="size-4" strokeWidth={1.8} />
            {copy(lang, {
              ar: "محادثات خاصة وآمنة",
              en: "Private & secure",
              ur: "نجی اور محفوظ",
            })}
          </div>
        </header>

        {/* Main inbox panel */}
        <div className="overflow-hidden rounded-[1.75rem] border border-border bg-card shadow-soft">
          {/* Toolbar */}
          <div className="border-b border-border p-4 sm:p-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
              {/* Search */}
              <div className="relative flex-1">
                <Search
                  className="pointer-events-none absolute start-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                  strokeWidth={1.8}
                />

                <input
                  type="search"
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  placeholder={copy(lang, {
                    ar: "ابحث في المحادثات...",
                    en: "Search conversations...",
                    ur: "گفتگو تلاش کریں...",
                  })}
                  className="
                    h-11 w-full rounded-xl
                    border border-border
                    bg-background
                    ps-11 pe-4
                    text-sm text-foreground
                    outline-none
                    transition
                    placeholder:text-muted-foreground/70
                    focus:border-primary/35
                    focus:ring-4 focus:ring-primary/[0.06]
                  "
                />
              </div>

              {/* Filters */}
              <div className="flex shrink-0 rounded-xl bg-stone-100 p-1">
                <button
                  type="button"
                  onClick={() => setFilter("all")}
                  className={`rounded-lg px-4 py-2 text-xs font-bold transition-all ${
                    filter === "all"
                      ? "bg-card text-foreground shadow-sm"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  {copy(lang, {
                    ar: "الكل",
                    en: "All",
                    ur: "سب",
                  })}
                </button>

                <button
                  type="button"
                  onClick={() => setFilter("unread")}
                  className={`inline-flex items-center gap-2 rounded-lg px-4 py-2 text-xs font-bold transition-all ${
                    filter === "unread"
                      ? "bg-card text-foreground shadow-sm"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  {copy(lang, {
                    ar: "غير مقروءة",
                    en: "Unread",
                    ur: "نہ پڑھے گئے",
                  })}

                  {unreadTotal > 0 && (
                    <span className="grid min-w-5 place-items-center rounded-full bg-primary px-1.5 py-0.5 text-[10px] font-extrabold text-primary-foreground">
                      {unreadTotal > 99 ? "99+" : unreadTotal}
                    </span>
                  )}
                </button>
              </div>
            </div>
          </div>

          {/* Loading */}
          {status === "loading" && (
            <div className="flex min-h-64 items-center justify-center">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-5 animate-spin" />
                {copy(lang, {
                  ar: "جارٍ تحميل المحادثات...",
                  en: "Loading conversations...",
                  ur: "گفتگو لوڈ ہو رہی ہے...",
                })}
              </div>
            </div>
          )}

          {/* Error */}
          {status === "error" && (
            <div className="flex min-h-64 flex-col items-center justify-center gap-3 px-6 text-center">
              <span className="grid size-11 place-items-center rounded-full bg-error-tint text-error">
                <AlertCircle className="size-5" />
              </span>

              <p className="text-sm font-semibold text-foreground">
                {copy(lang, {
                  ar: "تعذّر تحميل المحادثات",
                  en: "Couldn't load conversations",
                  ur: "گفتگو لوڈ نہیں ہو سکی",
                })}
              </p>

              <p className="max-w-md text-xs leading-6 text-muted-foreground">
                {loadError}
              </p>
            </div>
          )}

          {/* Empty */}
          {status === "success" && conversations.length === 0 && (
            <div className="flex min-h-[22rem] flex-col items-center justify-center px-6 text-center">
              <span className="grid size-14 place-items-center rounded-2xl bg-primary/[0.07] text-primary">
                <Inbox className="size-6" strokeWidth={1.7} />
              </span>

              <h2 className="mt-4 font-display text-lg font-bold text-foreground">
                {copy(lang, {
                  ar: "لا توجد محادثات بعد",
                  en: "No conversations yet",
                  ur: "ابھی کوئی گفتگو نہیں",
                })}
              </h2>

              <p className="mt-1.5 max-w-sm text-sm leading-6 text-muted-foreground">
                {copy(lang, {
                  ar: "عند تأكيد مطابقة وبدء التواصل، ستظهر المحادثة هنا.",
                  en: "When a match is confirmed and a conversation starts, it will appear here.",
                  ur: "میچ کی تصدیق اور گفتگو شروع ہونے پر وہ یہاں ظاہر ہوگی۔",
                })}
              </p>
            </div>
          )}

          {/* No search results */}
          {status === "success" &&
            conversations.length > 0 &&
            filteredConversations.length === 0 && (
              <div className="flex min-h-64 flex-col items-center justify-center px-6 text-center">
                <Search className="size-6 text-muted-foreground/50" />

                <p className="mt-3 text-sm font-semibold text-foreground">
                  {copy(lang, {
                    ar: "لا توجد نتائج",
                    en: "No results found",
                    ur: "کوئی نتیجہ نہیں ملا",
                  })}
                </p>

                <p className="mt-1 text-xs text-muted-foreground">
                  {copy(lang, {
                    ar: "جرّب البحث باسم مختلف أو اعرض جميع المحادثات.",
                    en: "Try another search or show all conversations.",
                    ur: "دوسرا نام تلاش کریں یا تمام گفتگو دکھائیں۔",
                  })}
                </p>
              </div>
            )}

          {/* Conversations */}
          {status === "success" && filteredConversations.length > 0 && (
            <div className="divide-y divide-border">
              {filteredConversations.map((conversation) => {
                const lastMessage = conversation.messages?.[0];
                const unreadCount = conversation.unreadCount || 0;
                const hasUnread = unreadCount > 0;

                return (
                  <Link
                    key={conversation.id}
                    to={`/messages/${conversation.id}`}
                    className={`
                      group relative flex items-center gap-4
                      px-4 py-4
                      transition-colors
                      sm:px-5
                      ${
                        hasUnread
                          ? "bg-primary/[0.025] hover:bg-primary/[0.045]"
                          : "hover:bg-stone-50/80"
                      }
                    `}
                  >
                    {/* Unread rail */}
                    {hasUnread && (
                      <span className="absolute inset-y-3 start-0 w-[3px] rounded-full bg-primary" />
                    )}

                    {/* Avatar */}
                    <div className="relative shrink-0">
                      <span
                        className={`
                          grid size-12 place-items-center
                          rounded-2xl
                          font-mono text-sm font-bold
                          ${
                            hasUnread
                              ? "bg-primary/10 text-primary"
                              : "bg-stone-100 text-foreground/65"
                          }
                        `}
                      >
                        {getInitials(conversation.otherParticipantName)}
                      </span>

                      {hasUnread && (
                        <span className="absolute -end-0.5 -top-0.5 size-3 rounded-full bg-accent ring-[3px] ring-card" />
                      )}
                    </div>

                    {/* Body */}
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-3">
                        <p
                          className={`min-w-0 flex-1 truncate text-sm text-foreground ${
                            hasUnread ? "font-extrabold" : "font-bold"
                          }`}
                        >
                          {conversation.otherParticipantName ||
                            copy(lang, {
                              ar: "مستخدم لُقيا",
                              en: "Luqya user",
                              ur: "لقیا صارف",
                            })}
                        </p>

                        {lastMessage && (
                          <time
                            className={`shrink-0 text-[11px] ${
                              hasUnread
                                ? "font-semibold text-primary"
                                : "text-muted-foreground"
                            }`}
                          >
                            {formatTime(lastMessage.creationTime, lang)}
                          </time>
                        )}
                      </div>

                      {/* Report context */}
                      <div className="mt-1 flex min-w-0 items-center gap-2">
                        <span
                          className={`
                            shrink-0 rounded-md px-2 py-0.5
                            text-[10px] font-bold
                            ${
                              conversation.reportType === ReportType.FOUND
                                ? "bg-success-tint text-success"
                                : "bg-warn-tint text-warn"
                            }
                          `}
                        >
                          {conversation.reportType === ReportType.FOUND
                            ? copy(lang, {
                                ar: "بلاغ العثور",
                                en: "Found report",
                                ur: "ملنے کی رپورٹ",
                              })
                            : copy(lang, {
                                ar: "بلاغ الفقد",
                                en: "Lost report",
                                ur: "گمشدگی کی رپورٹ",
                              })}
                        </span>

                        {conversation.reportDescription && (
                          <span className="min-w-0 truncate text-xs text-muted-foreground">
                            {conversation.reportDescription}
                          </span>
                        )}
                      </div>

                      {/* Last message */}
                      <div className="mt-2 flex items-center gap-3">
                        <p
                          className={`min-w-0 flex-1 truncate text-sm ${
                            hasUnread
                              ? "font-semibold text-foreground"
                              : "text-muted-foreground"
                          }`}
                        >
                          {lastMessage ? (
                            <>
                              {lastMessage.isMine && (
                                <span className="me-1 text-muted-foreground">
                                  {copy(lang, {
                                    ar: "أنت:",
                                    en: "You:",
                                    ur: "آپ:",
                                  })}
                                </span>
                              )}

                              {lastMessage.text}
                            </>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 text-xs">
                              <MessageCircle className="size-3.5" />
                              {copy(lang, {
                                ar: "ابدأ المحادثة",
                                en: "Start the conversation",
                                ur: "گفتگو شروع کریں",
                              })}
                            </span>
                          )}
                        </p>

                        {hasUnread && (
                          <span className="grid h-5 min-w-5 shrink-0 place-items-center rounded-full bg-primary px-1.5 text-[10px] font-extrabold leading-none text-primary-foreground">
                            {unreadCount > 9 ? "9+" : unreadCount}
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

        {/* Footer note */}
        <div className="mt-4 flex items-center justify-center gap-2 text-center text-[11px] text-muted-foreground">
          <ShieldCheck className="size-3.5 text-primary/70" />
          {copy(lang, {
            ar: "بيانات التواصل الشخصية لا تظهر للطرف الآخر.",
            en: "Personal contact details stay private.",
            ur: "ذاتی رابطے کی معلومات نجی رہتی ہیں۔",
          })}
        </div>
      </div>
    </section>
  );
}