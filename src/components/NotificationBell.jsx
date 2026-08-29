import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell,
  Sparkles,
  PhoneMissed,
  Loader2,
  CheckCircle2,
  CheckCheck,
  XCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import {
  listNotifications,
  listMyNotifications,
  markNotificationAsRead,
} from "../api/notifications";
import { getKnownReporterId } from "../api/reporterIdentity";
import { openConversation } from "../api/conversations";
import { getReport } from "../api/reports";
import { reportHeadingTitle } from "../lib/reportTitle";

const POLL_INTERVAL_MS = 30000;
const MAX_DISMISSED_IDS = 300;

function copy(lang, values) {
  return values[lang] ?? values.en;
}

function notificationKey(item) {
  return `${item.source ?? "unknown"}:${item.id}`;
}

function isPossibleMatchNotification(item) {
  const title = String(item?.title ?? "").trim().toLowerCase();

  return (
    title === "possible match found" ||
    title.includes("possible match") ||
    title.includes("match found")
  );
}

function isAcceptedNotification(item) {
  const title = String(item?.title ?? "").trim().toLowerCase();

  return (
    title === "match accepted" ||
    title.includes("match accepted") ||
    title.includes("accepted match")
  );
}

function isRejectedNotification(item) {
  const title = String(item?.title ?? "").trim().toLowerCase();
  const message = String(item?.message ?? "").trim().toLowerCase();

  return (
    title === "match rejected" ||
    title.includes("match rejected") ||
    title.includes("rejected match") ||
    message.includes("match was rejected") ||
    message.includes("match has been rejected")
  );
}

function isMissedCallNotification(item) {
  const title = String(item?.title ?? "").toLowerCase();
  const message = String(item?.message ?? "").toLowerCase();

  return title.includes("missed call") || message.includes("missed call");
}

function dismissedStorageKey(profile) {
  const identity =
    profile?.id ||
    profile?.userId ||
    profile?.userName ||
    profile?.email ||
    "signed-in-user";

  return `luqya:dismissed-notifications:${identity}`;
}

function readDismissedIds(profile) {
  try {
    const raw = window.localStorage.getItem(dismissedStorageKey(profile));
    const parsed = raw ? JSON.parse(raw) : [];
    return new Set(Array.isArray(parsed) ? parsed : []);
  } catch {
    return new Set();
  }
}

function writeDismissedIds(profile, keys) {
  try {
    window.localStorage.setItem(
      dismissedStorageKey(profile),
      JSON.stringify([...keys].slice(-MAX_DISMISSED_IDS))
    );
  } catch {
    // Clearing notifications should still work for this session even when
    // localStorage is unavailable.
  }
}

function groupNotifications(items) {
  const groups = new Map();

  for (const item of items) {
    if (isPossibleMatchNotification(item) && item.reportId) {
      const key = `match:${item.reportId}`;

      if (!groups.has(key)) {
        groups.set(key, {
          key,
          kind: "match-group",
          reportId: item.reportId,
          source: item.source,
          creationTime: item.creationTime,
          members: [],
        });
      }

      const group = groups.get(key);
      group.members.push(item);

      if (
        new Date(item.creationTime).getTime() >
        new Date(group.creationTime).getTime()
      ) {
        group.creationTime = item.creationTime;
      }

      continue;
    }

    groups.set(notificationKey(item), {
      ...item,
      key: notificationKey(item),
      kind: isAcceptedNotification(item)
        ? "accepted"
        : isRejectedNotification(item)
          ? "rejected"
          : isMissedCallNotification(item)
            ? "call"
            : "default",
      members: [item],
    });
  }

  return [...groups.values()]
    .map((group) => ({
      ...group,
      isRead: group.members.every((item) => item.isRead),
      unreadMembers: group.members.filter((item) => !item.isRead).length,
      count: group.members.length,
    }))
    .sort(
      (a, b) =>
        new Date(b.creationTime).getTime() -
        new Date(a.creationTime).getTime()
    );
}

function getLocalizedNotification(item, lang, reportTitle) {
  if (item.kind === "match-group") {
    const count = item.count || 1;

    if (lang === "ar") {
      return {
        title:
          count > 1
            ? `وجدنا ${count} تطابقات محتملة`
            : "وجدنا تطابقًا محتملًا",
        message: reportTitle
          ? `لبلاغك: ${reportTitle}. راجع المطابقات من لوحة التحكم.`
          : "وجدنا تطابقات محتملة لأحد بلاغاتك. راجعها من لوحة التحكم.",
        type: "match",
      };
    }

    if (lang === "ur") {
      return {
        title:
          count > 1
            ? `${count} ممکنہ میچ ملے`
            : "ممکنہ میچ ملا",
        message: reportTitle
          ? `آپ کی رپورٹ: ${reportTitle}۔ ڈیش بورڈ سے میچز کا جائزہ لیں۔`
          : "آپ کی ایک رپورٹ کے لیے ممکنہ میچز ملے ہیں۔ ڈیش بورڈ سے جائزہ لیں۔",
        type: "match",
      };
    }

    return {
      title:
        count > 1
          ? `${count} possible matches found`
          : "Possible match found",
      message: reportTitle
        ? `For your report: ${reportTitle}. Review the matches from your dashboard.`
        : "We found possible matches for one of your reports. Review them from your dashboard.",
      type: "match",
    };
  }

  if (lang !== "ar" && lang !== "ur") {
    return {
      title: item.title ?? "",
      message: item.message ?? "",
      type: item.kind ?? "default",
    };
  }

  if (item.kind === "accepted") {
    return {
      title: copy(lang, {
        ar: "تم تأكيد المطابقة",
        ur: "میچ کی تصدیق ہو گئی",
        en: "Match accepted",
      }),
      message: copy(lang, {
        ar: reportTitle
          ? `تم تأكيد مطابقة مرتبطة ببلاغك: ${reportTitle}.`
          : "تم تأكيد المطابقة من قِبل الطرف الآخر.",
        ur: reportTitle
          ? `آپ کی رپورٹ ${reportTitle} سے متعلق میچ کی تصدیق ہو گئی۔`
          : "دوسرے فریق نے میچ کی تصدیق کر دی۔",
        en: item.message ?? "",
      }),
      type: "accepted",
    };
  }

  if (item.kind === "rejected") {
    return {
      title: copy(lang, {
        ar: "تم رفض المطابقة",
        ur: "میچ مسترد کر دیا گیا",
        en: "Match rejected",
      }),
      message: copy(lang, {
        ar: reportTitle
          ? `تم رفض المطابقة المرتبطة ببلاغك: ${reportTitle}.`
          : "تم رفض المطابقة من قِبل الطرف الآخر.",
        ur: reportTitle
          ? `آپ کی رپورٹ ${reportTitle} سے متعلق میچ مسترد کر دیا گیا۔`
          : "دوسرے فریق نے میچ مسترد کر دیا۔",
        en: item.message ?? "Your match was rejected by the other party.",
      }),
      type: "rejected",
    };
  }

  if (item.kind === "call") {
    return {
      title: copy(lang, {
        ar: "مكالمة فائتة",
        ur: "چھوٹی ہوئی کال",
        en: "Missed call",
      }),
      message: copy(lang, {
        ar: "لديك مكالمة فائتة مرتبطة بهذه المحادثة.",
        ur: "اس گفتگو سے متعلق آپ کی ایک کال رہ گئی۔",
        en: item.message ?? "",
      }),
      type: "call",
    };
  }

  return {
    title: item.title ?? "",
    message: item.message ?? "",
    type: item.kind ?? "default",
  };
}

function NotificationIcon({ type }) {
  if (type === "call") {
    return (
      <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-xl bg-amber-50 text-amber-600">
        <PhoneMissed className="size-4" strokeWidth={1.8} />
      </span>
    );
  }

  if (type === "accepted") {
    return (
      <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
        <CheckCircle2 className="size-4" strokeWidth={1.8} />
      </span>
    );
  }

  if (type === "rejected") {
    return (
      <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-xl bg-error-tint text-error">
        <XCircle className="size-4" strokeWidth={1.8} />
      </span>
    );
  }

  return (
    <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
      <Sparkles className="size-4" strokeWidth={1.8} />
    </span>
  );
}

export default function NotificationBell() {
  const { t, lang, dir } = useI18n();
  const { profile } = useAuth();
  const navigate = useNavigate();

  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([]);
  const [reportTitles, setReportTitles] = useState({});
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [clearing, setClearing] = useState(false);

  const reporterId = getKnownReporterId();

  const loadNotifications = useCallback(
    async ({ showLoader = false } = {}) => {
      if (!profile) {
        setItems([]);
        setLoaded(false);
        return;
      }

      if (showLoader) setLoading(true);

      try {
        const requests = [
          listMyNotifications({
            maxResultCount: 50,
            sorting: "creationTime desc",
          })
            .then((res) =>
              (res?.items ?? []).map((n) => ({
                ...n,
                source: "identity",
              }))
            )
            .catch(() => []),

          reporterId
            ? listNotifications({
                reporterId,
                maxResultCount: 50,
                sorting: "creationTime desc",
              })
                .then((res) =>
                  (res?.items ?? []).map((n) => ({
                    ...n,
                    source: "reporter",
                  }))
                )
                .catch(() => [])
            : Promise.resolve([]),
        ];

        const [mine, byReporter] = await Promise.all(requests);

        const dismissed = readDismissedIds(profile);

        const merged = [...mine, ...byReporter]
          .filter((item) => !dismissed.has(notificationKey(item)))
          .sort(
            (a, b) =>
              new Date(b.creationTime).getTime() -
              new Date(a.creationTime).getTime()
          );

        setItems(merged);
        setLoaded(true);

        const reportIds = [
          ...new Set(
            merged
              .filter((item) => item.reportId)
              .map((item) => String(item.reportId))
          ),
        ];

        const missingReportIds = reportIds.filter(
          (reportId) => !reportTitles[reportId]
        );

        if (missingReportIds.length > 0) {
          const resolved = await Promise.all(
            missingReportIds.map(async (reportId) => {
              try {
                const report = await getReport(reportId);
                return [
                  reportId,
                  reportHeadingTitle(
                    report,
                    copy(lang, {
                      ar: "بلاغك",
                      en: "your report",
                      ur: "آپ کی رپورٹ",
                    })
                  ),
                ];
              } catch {
                return [reportId, null];
              }
            })
          );

          setReportTitles((current) => {
            const next = { ...current };

            for (const [reportId, title] of resolved) {
              if (title) next[reportId] = title;
            }

            return next;
          });
        }
      } finally {
        if (showLoader) setLoading(false);
      }
    },
    [lang, profile, reporterId, reportTitles]
  );

  // Load once immediately so the navbar dot does not depend on opening the menu.
  useEffect(() => {
    if (!profile) {
      setItems([]);
      setLoaded(false);
      return;
    }

    loadNotifications({ showLoader: false });
  }, [profile, reporterId]);

  // The backend has no push/SignalR notification stream here, so poll lightly
  // while signed in. This keeps the bell dot current even when the menu is closed.
  useEffect(() => {
    if (!profile) return;

    const intervalId = window.setInterval(() => {
      loadNotifications({ showLoader: false });
    }, POLL_INTERVAL_MS);

    function handleFocus() {
      loadNotifications({ showLoader: false });
    }

    window.addEventListener("focus", handleFocus);

    return () => {
      window.clearInterval(intervalId);
      window.removeEventListener("focus", handleFocus);
    };
  }, [profile, reporterId, loadNotifications]);

  useEffect(() => {
    if (open && !loaded) {
      loadNotifications({ showLoader: true });
    }
  }, [open, loaded, loadNotifications]);

  const groupedItems = useMemo(() => groupNotifications(items), [items]);

  // One unread match group = one unread notification in the UI, even when
  // the backend produced several raw match notifications for that report.
  const unreadCount = groupedItems.filter((item) => !item.isRead).length;

  async function markMembersAsRead(group) {
    const unreadMembers = group.members.filter((item) => !item.isRead);

    if (unreadMembers.length === 0) return;

    await Promise.allSettled(
      unreadMembers.map((item) => markNotificationAsRead(item.id))
    );

    const ids = new Set(unreadMembers.map((item) => item.id));

    setItems((prev) =>
      prev.map((item) =>
        ids.has(item.id) ? { ...item, isRead: true } : item
      )
    );
  }

  async function handleOpenItem(group) {
    await markMembersAsRead(group);

    setOpen(false);

    if (group.kind === "match-group") {
      navigate("/dashboard");
      return;
    }

    if (group.kind === "call" && group.reportId) {
      openConversation(group.reportId)
        .then((conversation) =>
          navigate(`/messages/${conversation.id}`)
        )
        .catch(() => navigate("/messages"));
      return;
    }

    if (group.kind === "accepted" || group.kind === "rejected") {
      navigate("/dashboard");
      return;
    }

    if (group.reportId) {
      navigate(`/match/${group.reportId}`);
    }
  }

  async function handleClearAll() {
    if (clearing || items.length === 0) return;

    setClearing(true);

    const snapshot = [...items];

    try {
      const unread = snapshot.filter((item) => !item.isRead);

      await Promise.allSettled(
        unread.map((item) => markNotificationAsRead(item.id))
      );

      const dismissed = readDismissedIds(profile);

      for (const item of snapshot) {
        dismissed.add(notificationKey(item));
      }

      writeDismissedIds(profile, dismissed);
      setItems([]);
    } finally {
      setClearing(false);
    }
  }

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label={t("navNotifications")}
        aria-expanded={open}
        className="
          relative grid size-10 place-items-center rounded-xl
          text-foreground/55 transition-all duration-200
          hover:bg-stone-100 hover:text-foreground
          focus-visible:outline-none focus-visible:ring-2
          focus-visible:ring-primary/20
        "
      >
        <Bell className="size-[18px]" strokeWidth={1.8} />

        {unreadCount > 0 && (
          <span
            aria-label={copy(lang, {
              ar: "لديك إشعارات جديدة",
              en: "You have new notifications",
              ur: "آپ کے پاس نئی اطلاعات ہیں",
            })}
            className="
              absolute end-1.5 top-1.5 size-2.5 rounded-full
              bg-accent ring-2 ring-background
            "
          />
        )}
      </button>

      {open && (
        <>
          <div
            className="fixed inset-0 z-40"
            onClick={() => setOpen(false)}
          />

          <div
            dir={dir}
            role="dialog"
            aria-label={t("notifTitle")}
            className="
              fixed inset-x-2 top-[4.5rem] z-[60] mx-auto
              w-auto max-w-none
              overflow-hidden rounded-2xl
              border border-border/90 bg-card/98
              shadow-luxe backdrop-blur-xl
              animate-rise-in

              sm:absolute sm:inset-x-auto sm:end-0 sm:top-full
              sm:mx-0 sm:mt-3 sm:w-[23rem] sm:max-w-[calc(100vw-1.5rem)]
              sm:rounded-[1.5rem]
            "
          >
            <div className="
              sticky top-0 z-10 flex items-center justify-between gap-3
              border-b border-border/80 bg-card/95 px-4 py-3.5 backdrop-blur-xl
              sm:static sm:bg-transparent sm:px-5 sm:py-4 sm:backdrop-blur-none
            ">
              <div className="min-w-0">
                <span className="block text-[15px] font-extrabold text-foreground sm:text-sm">
                  {t("notifTitle")}
                </span>

                <span className="mt-0.5 block text-[11px] font-medium text-muted-foreground">
                  {unreadCount > 0
                    ? copy(lang, {
                        ar: `${unreadCount} غير مقروء`,
                        en: `${unreadCount} unread`,
                        ur: `${unreadCount} بغیر پڑھے`,
                      })
                    : copy(lang, {
                        ar: "لا توجد إشعارات جديدة",
                        en: "No new notifications",
                        ur: "کوئی نئی اطلاع نہیں",
                      })}
                </span>
              </div>

              {groupedItems.length > 0 && (
                <button
                  type="button"
                  onClick={handleClearAll}
                  disabled={clearing}
                  className="
                    inline-flex min-h-8 items-center gap-1.5 rounded-lg
                    px-2.5 text-[11px] font-bold text-muted-foreground
                    transition-colors hover:bg-stone-100 hover:text-foreground
                    focus-visible:outline-none focus-visible:ring-2
                    focus-visible:ring-primary/15
                    disabled:pointer-events-none disabled:opacity-50

                    sm:min-h-9 sm:rounded-xl sm:px-3 sm:text-xs
                  "
                >
                  {clearing ? (
                    <Loader2 className="size-3.5 animate-spin" />
                  ) : (
                    <CheckCheck className="size-3.5" />
                  )}

                  {copy(lang, {
                    ar: "مسح الكل",
                    en: "Clear all",
                    ur: "سب صاف کریں",
                  })}
                </button>
              )}
            </div>

            <div
              className="
                max-h-[calc(100dvh-8.75rem)] overflow-y-auto overscroll-contain
                [scrollbar-width:none] [&::-webkit-scrollbar]:hidden
                sm:max-h-[28rem]
              "
            >
              {!profile ? (
                <div className="px-5 py-8 text-center text-sm text-muted-foreground sm:py-9">
                  {t("navLogin")}
                </div>
              ) : loading && !loaded ? (
                <div className="flex items-center justify-center px-5 py-9 sm:py-10">
                  <Loader2 className="size-5 animate-spin text-primary" />
                </div>
              ) : groupedItems.length === 0 ? (
                <div className="px-5 py-9 text-center sm:px-6 sm:py-10">
                  <span className="mx-auto grid size-11 place-items-center rounded-2xl bg-primary/[0.06] text-primary">
                    <Bell className="size-4" />
                  </span>

                  <p className="mt-3 text-sm font-bold text-foreground">
                    {copy(lang, {
                      ar: "أنت على اطلاع بكل شيء",
                      en: "You're all caught up",
                      ur: "آپ سب کچھ دیکھ چکے ہیں",
                    })}
                  </p>

                  <p className="mt-1 text-xs text-muted-foreground">
                    {copy(lang, {
                      ar: "ستظهر الإشعارات الجديدة هنا.",
                      en: "New notifications will appear here.",
                      ur: "نئی اطلاعات یہاں ظاہر ہوں گی۔",
                    })}
                  </p>
                </div>
              ) : (
                <ul>
                  {groupedItems.map((item) => {
                    const reportTitle = item.reportId
                      ? reportTitles[String(item.reportId)]
                      : null;

                    const localized = getLocalizedNotification(
                      item,
                      lang,
                      reportTitle
                    );

                    return (
                      <li key={item.key}>
                        <button
                          type="button"
                          onClick={() => handleOpenItem(item)}
                          className={`
                            group flex w-full items-start gap-3
                            border-b border-border/70 px-4 py-4 text-start
                            transition-colors last:border-b-0
                            active:bg-primary/[0.04]
                            hover:bg-primary/[0.025]
                            focus-visible:outline-none
                            focus-visible:bg-primary/[0.04]

                            sm:gap-3 sm:px-5 sm:py-4
                            ${
                              item.isRead
                                ? "bg-card"
                                : "bg-primary/[0.018]"
                            }
                          `}
                        >
                          <NotificationIcon type={localized.type} />

                          <span className="min-w-0 flex-1">
                            <span className="flex items-start gap-2">
                              <span className="min-w-0 flex-1">
                                <span
                                  className={`block text-sm leading-[1.45] text-foreground sm:text-sm ${
                                    item.isRead
                                      ? "font-semibold"
                                      : "font-extrabold"
                                  }`}
                                >
                                  {localized.title}
                                </span>

                                {localized.message && (
                                  <span className="mt-1.5 block line-clamp-2 text-[12px] leading-5 text-muted-foreground sm:mt-1 sm:text-[12px] sm:leading-5">
                                    {localized.message}
                                  </span>
                                )}
                              </span>

                              {!item.isRead && (
                                <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" />
                              )}
                            </span>

                            {item.kind === "match-group" && item.count > 1 && (
                              <span className="mt-2.5 inline-flex rounded-lg bg-primary/[0.06] px-2.5 py-1 text-[10px] font-bold text-primary sm:mt-2 sm:rounded-full">
                                <span className="sm:hidden">
                                  {copy(lang, {
                                    ar: `${item.count} مطابقات`,
                                    en: `${item.count} matches`,
                                    ur: `${item.count} میچ`,
                                  })}
                                </span>
                                <span className="hidden sm:inline">
                                  {copy(lang, {
                                    ar: `${item.count} مطابقات في إشعار واحد`,
                                    en: `${item.count} matches grouped`,
                                    ur: `${item.count} میچ ایک اطلاع میں`,
                                  })}
                                </span>
                              </span>
                            )}
                          </span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
