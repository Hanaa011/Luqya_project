import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Bell, Sparkles, PhoneMissed, Loader2 } from "lucide-react";
import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { listNotifications, listMyNotifications, markNotificationAsRead } from "../api/notifications";
import { getKnownReporterId } from "../api/reporterIdentity";
import { openConversation } from "../api/conversations";

/**
 * Two independent notification sources, merged here rather than on the
 * backend (there is no single owner id that covers both — see
 * Notification.cs's dual ReporterId/IdentityUserId keys):
 *  - GetListAsync(reporterId, ...): reporter-keyed (e.g. match alerts).
 *    Only callable when a real reporterId is already known (cached after
 *    this browser created a report — see api/reporterIdentity.js). Never
 *    sends Guid.Empty or a guess.
 *  - GetMyListAsync(...): identity-keyed (currently: missed calls), scoped
 *    server-side to CurrentUser.Id. Fetched whenever the user is logged
 *    in, independent of whether a reporterId is known.
 * Items from the identity-keyed source are tagged source:"identity" so a
 * click can open the related conversation; reporter-keyed items keep the
 * existing mark-as-read-only behavior.
 */
export default function NotificationBell() {
  const { t } = useI18n();
  const { profile } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);

  const reporterId = getKnownReporterId();

  useEffect(() => {
    if (!open || loaded || !profile) return;

    let cancelled = false;
    Promise.resolve().then(() => {
      if (!cancelled) setLoading(true);
    });

    const requests = [
      listMyNotifications({ maxResultCount: 20, sorting: "creationTime desc" })
        .then((res) => (res?.items ?? []).map((n) => ({ ...n, source: "identity" })))
        .catch(() => []),
      reporterId
        ? listNotifications({ reporterId, maxResultCount: 20, sorting: "creationTime desc" })
            .then((res) => (res?.items ?? []).map((n) => ({ ...n, source: "reporter" })))
            .catch(() => [])
        : Promise.resolve([]),
    ];

    Promise.all(requests)
      .then(([mine, byReporter]) => {
        if (cancelled) return;
        const merged = [...mine, ...byReporter].sort(
          (a, b) => new Date(b.creationTime) - new Date(a.creationTime)
        );
        setItems(merged);
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
        setLoaded(true);
      });

    return () => {
      cancelled = true;
    };
  }, [open, loaded, profile, reporterId]);

  const unreadCount = items.filter((n) => !n.isRead).length;

  function handleOpenItem(item) {
    if (!item.isRead) {
      markNotificationAsRead(item.id)
        .then(() => setItems((prev) => prev.map((n) => (n.id === item.id ? { ...n, isRead: true } : n))))
        .catch(() => {});
    }

    if (item.source === "identity" && item.reportId) {
      setOpen(false);
      openConversation(item.reportId)
        .then((conversation) => navigate(`/messages/${conversation.id}`))
        .catch(() => {});
    }
  }

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label={t("navNotifications")}
        className="relative size-10 rounded-full border border-border grid place-items-center hover:bg-stone-100 transition-colors"
      >
        <Bell className="size-4 text-foreground/70" />
        {unreadCount > 0 && (
          <span className="absolute top-2 end-2 size-2 rounded-full bg-accent ring-2 ring-background" />
        )}
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />

          <div className="absolute end-0 top-full mt-3 w-80 z-50 rounded-2xl border border-border bg-card shadow-luxe overflow-hidden animate-rise-in">
            <div className="px-5 py-4 border-b border-border flex items-center justify-between">
              <span className="text-sm font-bold">{t("notifTitle")}</span>
              <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                {t("notifLive")}
              </span>
            </div>

            {!profile ? (
              <div className="px-5 py-8 text-center text-sm text-muted-foreground">{t("navLogin")}</div>
            ) : loading ? (
              <div className="px-5 py-8 flex items-center justify-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
              </div>
            ) : items.length === 0 ? (
              <div className="px-5 py-8 text-center text-sm text-muted-foreground">{t("notifEmpty")}</div>
            ) : (
              <ul>
                {items.map((item) => (
                  <li
                    key={item.id}
                    onClick={() => handleOpenItem(item)}
                    className={`px-5 py-4 flex items-start gap-3 border-b border-border last:border-0 hover:bg-stone-50 transition-colors cursor-pointer ${
                      item.isRead ? "opacity-60" : ""
                    }`}
                  >
                    <span className="mt-0.5 size-8 rounded-xl grid place-items-center shrink-0 bg-primary/10 text-primary">
                      {item.source === "identity" ? (
                        <PhoneMissed className="size-4" />
                      ) : (
                        <Sparkles className="size-4" />
                      )}
                    </span>

                    <span className="flex-1 text-sm leading-snug">
                      <span className="block font-semibold">{item.title}</span>
                      {item.message}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </div>
  );
}
