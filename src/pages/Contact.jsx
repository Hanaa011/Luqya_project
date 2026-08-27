import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { AlertCircle, Loader2 } from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { openConversation } from "../api/conversations";

function copy(lang, values) {
  return values[lang] ?? values.en;
}

// Privacy fix: this legacy route used to render the reporter's raw
// phone/email. In-platform conversations (see Match.jsx's own
// openConversationAndGo) are now the only way to reach a reporter, so this
// page no longer displays contact info at all - it just opens (or reuses)
// the private conversation for this report and forwards there. This closes
// the leak for direct/typed navigation to this URL too, not just the
// removed UI entry points.
export default function Contact() {
  const { id } = useParams();
  const { lang } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();

  const [error, setError] = useState(null);

  useEffect(() => {
    document.title = "Contact reporter — Luqya";
  }, []);

  useEffect(() => {
    let cancelled = false;

    openConversation(id)
      .then((conversation) => {
        if (cancelled) return;
        navigate(`/messages/${conversation.id}`, { replace: true });
      })
      .catch((err) => {
        if (cancelled) return;
        setError(
          err.message ||
            copy(lang, {
              ar: "تعذّر فتح المحادثة. حاول مرة أخرى.",
              en: "Couldn't open the conversation. Please try again.",
              ur: "بات چیت شروع نہیں ہو سکی۔ دوبارہ کوشش کریں۔",
            })
        );
      });

    return () => {
      cancelled = true;
    };
  }, [id, lang, navigate]);

  function goBack() {
    if (location.key !== "default") {
      navigate(-1);
    } else {
      navigate(`/match/${id}`);
    }
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 py-32 px-6 text-center">
        <AlertCircle className="size-7 text-error" />
        <p className="max-w-md text-sm text-muted-foreground">{error}</p>
        <button type="button" onClick={goBack} className="mt-2 text-sm font-semibold text-primary hover:underline">
          {copy(lang, { ar: "العودة إلى البلاغ", en: "Back to report", ur: "رپورٹ پر واپس جائیں" })}
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center justify-center gap-2 py-32 text-muted-foreground">
      <Loader2 className="size-5 animate-spin" />
      {copy(lang, { ar: "جارٍ فتح المحادثة...", en: "Opening the conversation...", ur: "بات چیت کھولی جا رہی ہے..." })}
    </div>
  );
}
