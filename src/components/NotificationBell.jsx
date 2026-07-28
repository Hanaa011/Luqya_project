import { useEffect, useState } from "react";
import { Bell, Sparkles, Loader2, BellOff } from "lucide-react";
import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import { listNotifications, markNotificationAsRead } from "../api/notifications";
import { getKnownReporterId } from "../api/reporterIdentity";

/**
 * NotificationAppService.GetListAsync(Guid reporterId, ...) — reporterId
 * is a real, non-nullable required parameter (verified in
 * LostFound.Application/Notifications/NotificationAppService.cs). There is
 * no "get my reporter" endpoint, so this only calls the API when a real
 * reporterId is already known (cached after this browser created a report
 * — see api/reporterIdentity.js). It never sends Guid.Empty or a guess.
 */
export default function NotificationBell() {
  const { t } = useI18n();
  const { profile } = useAuth();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);

  const reporterId = getKnownReporterId();

  useEffect(() => {
    if (!open || loaded || !profile || !reporterId) return;

    let cancelled = false;
    Promise.resolve().then(() => {
      if (!cancelled) setLoading(true);
    });

    listNotifications({ reporterId, maxResultCount: 20, sorting: "creationTime desc" })
      .then((res) => !cancelled && setItems(res?.items ?? []))
      .catch(() => !cancelled && setItems([]))
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
    if (item.isRead) return;
    markNotificationAsRead(item.id)
      .then(() => setItems((prev) => prev.map((n) => (n.id === item.id ? { ...n, isRead: true } : n))))
      .catch(() => {});
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
            ) : !reporterId ? (
              <div className="px-5 py-8 flex flex-col items-center gap-2 text-center text-sm text-muted-foreground">
                <BellOff className="size-5" />
                {t("notifUnavailable")}
              </div>
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
                      <Sparkles className="size-4" />
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
