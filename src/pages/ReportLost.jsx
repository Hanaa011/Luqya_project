import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  Upload,
  Sparkles,
  MapPin,
  Calendar,
  Mic,
  Check,
  Search,
  ScanEye,
  ListOrdered,
  BellRing,
  ArrowLeft,
  AlertCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import DammaMark from "../components/DammaMark";
import GuestContactFields from "../components/GuestContactFields";
import { createReport } from "../api/reports";
import { listLocations, createLocation } from "../api/locations";
import { aiSearch, imageFileToBase64 } from "../api/search";
import { ReportType, PreferredContactType } from "../api/enums";
import { ApiError } from "../api/httpClient";
import { isValidSaudiMobile } from "../lib/saudiPhone";
import { setKnownReporterId } from "../api/reporterIdentity";
import { fetchMyReports } from "../lib/myReports";
import { buildReporterFields } from "../lib/reporterFields";

const STAGES = [
  { key: "saved", labelKey: "stageSaved", Icon: Check },
  { key: "analyze", labelKey: "stageAnalyze", Icon: ScanEye },
  { key: "semantic", labelKey: "stageSemantic", Icon: Search },
  { key: "matching", labelKey: "stageMatching", Icon: DammaMark },
  { key: "ranking", labelKey: "stageRanking", Icon: ListOrdered },
];

// The pace the UI reveals each stage at, independent of when the real
// network calls actually finish — the last stage ("matching") holds until
// the real API response arrives instead of a fixed timeout, so the
// animation never claims to be done before the work actually is.
const STAGE_DELAY_MS = 700;

export default function ReportLost() {
  const { t, tr, lang, dir } = useI18n();
  const { profile, userId } = useAuth();

  const [phase, setPhase] = useState("form"); // form | searching | results | empty
  const [stageIndex, setStageIndex] = useState(0);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [locationText, setLocationText] = useState("");
  const [lostFoundDate, setLostFoundDate] = useState("");
  const [imageFile, setImageFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [matches, setMatches] = useState([]);
  const [errorMsg, setErrorMsg] = useState(null);
  const [knownLocations, setKnownLocations] = useState([]);
  const [guest, setGuest] = useState({
    reporterName: "",
    reporterPhone: "",
    reporterEmail: "",
    preferredContact: PreferredContactType.PHONE,
  });
  const [phoneError, setPhoneError] = useState(false);
  const [profilePhone, setProfilePhone] = useState("");
  const [profilePhoneError, setProfilePhoneError] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    document.title = tr({
      ar: "الإبلاغ عن مفقود — لُقيا",
      en: "Report a lost item — Luqya",
      ur: "گمشدہ چیز کی رپورٹ — لقیا",
    });
  }, [lang, tr]);

  useEffect(() => {
    listLocations({ maxResultCount: 200 })
      .then((res) => setKnownLocations(res?.items ?? []))
      .catch(() => setKnownLocations([]));
  }, []);

  useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
  }, [preview]);

  const CATEGORY_OPTIONS = {
    ar: ["إلكترونيات", "حقائب", "وثائق", "مجوهرات"],
    en: ["Electronics", "Bags", "Documents", "Jewelry"],
    ur: ["الیکٹرانکس", "بیگز", "دستاویزات", "زیورات"],
  };
  const suggestions = description.length > 8 ? CATEGORY_OPTIONS[lang] ?? CATEGORY_OPTIONS.en : [];

  function handleFile(file) {
    if (preview) URL.revokeObjectURL(preview);
    setImageFile(file);
    setPreview(file ? URL.createObjectURL(file) : null);
  }

  function updateGuest(field, value) {
    setGuest((g) => ({ ...g, [field]: value }));
    if (field === "reporterPhone") setPhoneError(false);
  }

  async function resolveLocationId(placeName) {
    const existing = knownLocations.find(
      (loc) => loc.placeName?.trim().toLowerCase() === placeName.trim().toLowerCase()
    );
    if (existing) return existing.id;
    const created = await createLocation({ placeName });
    return created.id;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (submitting) return; // guard against duplicate submits
    setErrorMsg(null);

    if (!profile && !isValidSaudiMobile(guest.reporterPhone)) {
      setPhoneError(true);
      return;
    }

    const profileHasPhone = Boolean((profile?.phoneNumber || "").trim());
    if (profile && !profileHasPhone && !isValidSaudiMobile(profilePhone)) {
      setProfilePhoneError(true);
      return;
    }

    setSubmitting(true);
    setPhase("searching");
    setStageIndex(0);

    const stageTimer = window.setInterval(() => {
      setStageIndex((i) => Math.min(i + 1, STAGES.length - 2)); // holds at "matching"
    }, STAGE_DELAY_MS);

    try {
      const locationId = await resolveLocationId(locationText || "—");

      const report = await createReport({
        locationId,
        locationDetails: locationText,
        type: ReportType.LOST,
        description: `${title ? title + " — " : ""}${description}`,
        lostFoundDate: lostFoundDate ? new Date(lostFoundDate).toISOString() : undefined,
        isItemWithFinder: false,
        ...buildReporterFields({
          profile: profile && !profileHasPhone ? { ...profile, phoneNumber: profilePhone } : profile,
          guest,
        }),
      });

      if (report?.reporterId) setKnownReporterId(report.reporterId);

      const imageBase64 = await imageFileToBase64(imageFile);

      // Searching within FOUND reports for something matching what was lost.
      let results = await aiSearch({
        text: description,
        imageBase64,
        type: ReportType.FOUND,
        maxResults: 6,
      });

      // Exclude the user's own reports from recovery candidates — only
      // when ownership can be verified via the real creatorId audit
      // field, not a guess. Same mechanism as SmartSearch.jsx.
      if (userId && results && results.length > 0) {
        const mine = await fetchMyReports({ userId });
        if (mine.reliable && mine.reports.length > 0) {
          const myIds = new Set(mine.reports.map((r) => r.id));
          results = results.filter((r) => !myIds.has(r.reportId));
        }
      }

      window.clearInterval(stageTimer);
      setStageIndex(STAGES.length - 1);

      window.setTimeout(() => {
        if (results && results.length > 0) {
          setMatches(results);
          setPhase("results");
        } else {
          setPhase("empty");
        }
        setSubmitting(false);
      }, STAGE_DELAY_MS);
    } catch (err) {
      window.clearInterval(stageTimer);
      setSubmitting(false);

      if (err instanceof ApiError && err.isUnauthorized) {
        setErrorMsg(
          tr({
            ar: "انتهت جلستك. سجّل الدخول مرة أخرى.",
            en: "Your session expired. Please log in again.",
            ur: "آپ کا سیشن ختم ہو گیا۔ دوبارہ لاگ ان کریں۔",
          })
        );
      } else {
        setErrorMsg(
          err.message ||
            tr({
              ar: "تعذّر إرسال البلاغ. حاول مرة أخرى.",
              en: "Couldn't submit the report. Please try again.",
              ur: "رپورٹ جمع نہیں ہو سکی۔ دوبارہ کوشش کریں۔",
            })
        );
      }
      setPhase("form");
    }
  }

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-4xl mx-auto px-6">
        {phase === "form" && (
          <FormView
            t={t}
            dir={dir}
            title={title}
            setTitle={setTitle}
            description={description}
            setDescription={setDescription}
            locationText={locationText}
            setLocationText={setLocationText}
            lostFoundDate={lostFoundDate}
            setLostFoundDate={setLostFoundDate}
            suggestions={suggestions}
            preview={preview}
            handleFile={handleFile}
            handleSubmit={handleSubmit}
            errorMsg={errorMsg}
            isGuest={!profile}
            guest={guest}
            updateGuest={updateGuest}
            phoneError={phoneError}
            needsProfilePhone={Boolean(profile) && !(profile?.phoneNumber || "").trim()}
            profilePhone={profilePhone}
            setProfilePhone={(v) => {
              setProfilePhone(v);
              setProfilePhoneError(false);
            }}
            profilePhoneError={profilePhoneError}
            submitting={submitting}
          />
        )}

        {phase === "searching" && (
          <SearchingView t={t} stageIndex={stageIndex} preview={preview} />
        )}

        {phase === "results" && <MatchesView t={t} lang={lang} matches={matches} />}
        {phase === "empty" && <EmptyView t={t} />}
      </div>
    </section>
  );
}

function Field({ label, hint, icon: Icon, children }) {
  return (
    <div>
      <div className="flex items-center justify-between mb-2.5">
        <label className="text-sm font-semibold flex items-center gap-2">
          {Icon && <Icon className="size-3.5 text-muted-foreground" />}
          {label}
        </label>
        {hint && (
          <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground/70">
            {hint}
          </span>
        )}
      </div>
      {children}
    </div>
  );
}

function FormView({
  t,
  dir,
  title,
  setTitle,
  description,
  setDescription,
  locationText,
  setLocationText,
  lostFoundDate,
  setLostFoundDate,
  suggestions,
  preview,
  handleFile,
  handleSubmit,
  errorMsg,
  isGuest,
  guest,
  updateGuest,
  phoneError,
  needsProfilePhone,
  profilePhone,
  setProfilePhone,
  profilePhoneError,
  submitting,
}) {
  return (
    <div className="animate-rise-in">
      <Link
        to="/report"
        className="inline-flex items-center gap-2 text-sm font-semibold text-muted-foreground hover:text-primary mb-8 transition-colors"
      >
        <ArrowLeft className={`size-4 ${dir === "rtl" ? "rotate-180" : ""}`} />
        {t("backLabel")}
      </Link>

      <div className="mb-10">
        <div className="inline-flex items-center gap-2 text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
          <DammaMark className="size-3.5" />
          {t("lostEyebrow")}
        </div>
        <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight mb-3">
          {t("lostTitle")}
        </h1>
        <p className="text-muted-foreground text-lg">{t("lostSub")}</p>
      </div>

      {errorMsg && (
        <div className="mb-6 flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
          <AlertCircle className="size-4 shrink-0 mt-0.5" />
          <span>{errorMsg}</span>
        </div>
      )}

      <form
        onSubmit={handleSubmit}
        className="bg-card border border-border rounded-[2rem] p-8 lg:p-12 shadow-soft space-y-8"
      >
        <Field label={t("fldTitle")}>
          <input
            type="text"
            required
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t("fldTitlePh")}
            className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
          />
        </Field>

        <Field label={t("fldDesc")} hint={t("aiSuggests")}>
          <div className="relative">
            <textarea
              required
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={t("fldDescPh")}
              rows={5}
              className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all resize-none"
            />
            <button
              type="button"
              className="absolute bottom-4 end-4 size-10 rounded-full bg-card border border-border grid place-items-center hover:bg-stone-100 transition-colors"
              aria-label={t("voiceInputLabel")}
            >
              <Mic className="size-4 text-muted-foreground" />
            </button>
          </div>

          {suggestions.length > 0 && (
            <div className="flex flex-wrap gap-2 mt-3 animate-fade-up">
              {suggestions.map((s) => (
                <span
                  key={s}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-primary/5 text-primary text-xs font-semibold ring-1 ring-primary/10"
                >
                  <Sparkles className="size-3" />
                  {s}
                </span>
              ))}
            </div>
          )}
        </Field>

        <div className="grid md:grid-cols-2 gap-5">
          <Field label={t("fldLocation")} icon={MapPin}>
            <input
              type="text"
              required
              value={locationText}
              onChange={(e) => setLocationText(e.target.value)}
              placeholder={t("fldLocationPh")}
              className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
            />
          </Field>

          <Field label={t("fldDate")} icon={Calendar}>
            <input
              type="date"
              value={lostFoundDate}
              onChange={(e) => setLostFoundDate(e.target.value)}
              className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
            />
          </Field>
        </div>

        <Field label={t("fldPhoto")}>
          <label className="block relative rounded-2xl border-2 border-dashed border-stone-200 hover:border-primary/40 hover:bg-primary/[0.02] transition-colors cursor-pointer overflow-hidden">
            {preview ? (
              <img src={preview} alt="" className="w-full h-64 object-cover" />
            ) : (
              <div className="py-16 flex flex-col items-center gap-3">
                <div className="size-14 rounded-2xl bg-primary/5 text-primary grid place-items-center">
                  <Upload className="size-6" strokeWidth={1.5} />
                </div>
                <p className="text-sm text-muted-foreground">{t("photoHint")}</p>
              </div>
            )}
            <input
              type="file"
              accept="image/*"
              onChange={(e) => handleFile(e.target.files?.[0] ?? null)}
              className="hidden"
            />
          </label>
        </Field>

        {isGuest && (
          <GuestContactFields t={t} tone="primary" values={guest} onChange={updateGuest} phoneError={phoneError} />
        )}

        {needsProfilePhone && (
          <Field label={t("fldMobile")}>
            <input
              type="tel"
              dir="ltr"
              value={profilePhone}
              onChange={(e) => setProfilePhone(e.target.value)}
              placeholder={t("fldMobilePh")}
              className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all text-start"
            />
            <p className="text-xs text-muted-foreground mt-2">{t("profileNoPhoneHint")}</p>
            {profilePhoneError && (
              <p className="text-xs text-error mt-1">{t("fldMobileInvalid")}</p>
            )}
          </Field>
        )}

        <div className="flex justify-end pt-4">
          <button
            type="submit"
            disabled={submitting}
            className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-8 py-4 rounded-2xl font-semibold shadow-luxe hover:-translate-y-0.5 transition-transform disabled:opacity-70 disabled:translate-y-0"
          >
            <Sparkles className="size-4" />
            {t("lostSubmitCta")}
          </button>
        </div>
      </form>
    </div>
  );
}

function SearchingView({ t, stageIndex, preview }) {
  const progress = ((stageIndex + 1) / STAGES.length) * 100;

  return (
    <div className="max-w-xl mx-auto text-center py-10 animate-rise-in">
      <div className="relative size-40 mx-auto mb-10">
        <div className="absolute inset-0 rounded-[2rem] bg-gradient-to-br from-primary/10 to-accent/10 overflow-hidden">
          {preview ? (
            <img src={preview} alt="" className="w-full h-full object-cover opacity-70" />
          ) : (
            <div className="w-full h-full grid place-items-center">
              <DammaMark className="size-10 text-primary" glow />
            </div>
          )}
          <div className="absolute inset-x-0 h-1/3 bg-gradient-to-b from-transparent via-primary/40 to-transparent animate-scan-line" />
        </div>
        <div className="absolute -inset-2 rounded-[2.2rem] border border-primary/20 animate-glow-pulse" />
      </div>

      <div className="text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-3">
        {t("aiAtWork")}
      </div>

      <h2 className="font-display text-2xl lg:text-3xl font-bold mb-10 min-h-10">
        {t(STAGES[stageIndex].labelKey)}
      </h2>

      <div className="h-1.5 rounded-full bg-stone-100 overflow-hidden mb-10">
        <div
          className="h-full bg-gradient-to-r from-primary to-accent rounded-full transition-all duration-700 ease-out"
          style={{ width: `${progress}%` }}
        />
      </div>

      <div className="flex justify-center gap-6">
        {STAGES.map((stage, index) => {
          const Icon = stage.Icon;
          const done = index < stageIndex;
          const active = index === stageIndex;
          return (
            <div key={stage.key} className="flex flex-col items-center gap-2">
              <div
                className={`size-10 rounded-full grid place-items-center border transition-all ${
                  done
                    ? "bg-primary text-primary-foreground border-primary"
                    : active
                    ? "border-primary text-primary bg-primary/5"
                    : "border-border text-muted-foreground/40"
                }`}
              >
                {stage.key === "matching" ? (
                  <DammaMark className="size-4" spin={active} />
                ) : (
                  <Icon className="size-4" />
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function MatchesView({ t, matches }) {
  return (
    <div className="animate-rise-in">
      <div className="text-center mb-12">
        <div className="size-14 mx-auto rounded-2xl bg-primary text-primary-foreground grid place-items-center mb-5 shadow-glow">
          <Sparkles className="size-6" />
        </div>
        <div className="text-[11px] font-mono uppercase tracking-widest text-primary font-bold mb-2">
          {t("searchComplete")}
        </div>
        <h2 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight mb-3">
          {t("matchesFoundTitle")}
        </h2>
        <p className="text-muted-foreground">{t("matchesFoundSub")}</p>
      </div>

      <div className="grid sm:grid-cols-2 gap-5">
        {matches.map((m) => (
          <Link
            key={m.reportId}
            to={`/match/${m.reportId}`}
            className="group rounded-[1.75rem] border border-border bg-card overflow-hidden shadow-soft hover:shadow-luxe hover:-translate-y-1 transition-all"
          >
            <div className="aspect-[16/10] bg-gradient-to-br from-stone-100 to-stone-200 relative grid place-items-center">
              <DammaMark className="size-9 text-primary/40" />
              <span className="absolute top-3 end-3 bg-primary text-primary-foreground text-xs font-bold font-mono px-2.5 py-1 rounded-full">
                {Math.round(m.scorePercentage)}%
              </span>
            </div>
            <div className="p-5">
              <h3 className="font-bold mb-2 group-hover:text-primary transition-colors">
                {m.description}
              </h3>
              <div className="flex items-center gap-3 text-xs text-muted-foreground">
                {m.aiObjectType && (
                  <span className="inline-flex items-center gap-1">
                    <MapPin className="size-3" />
                    {m.aiObjectType}
                  </span>
                )}
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}

function EmptyView({ t }) {
  return (
    <div className="max-w-lg mx-auto text-center py-10 animate-rise-in">
      <div className="relative size-24 mx-auto mb-8">
        <div className="absolute inset-0 rounded-full bg-primary/10 animate-ping-slow" />
        <div className="relative size-24 rounded-full bg-primary/5 border border-primary/20 grid place-items-center">
          <BellRing className="size-9 text-primary" strokeWidth={1.5} />
        </div>
      </div>

      <h2 className="font-display text-2xl lg:text-3xl font-bold mb-4">{t("emptyTitle")}</h2>

      <p className="text-muted-foreground leading-relaxed mb-10">{t("emptyBody")}</p>

      <div className="flex flex-wrap justify-center gap-3">
        <Link
          to="/browse"
          className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-6 py-3.5 rounded-2xl font-semibold shadow-glow hover:-translate-y-0.5 transition-transform"
        >
          {t("browseReportsCta")}
        </Link>
        <Link
          to="/dashboard"
          className="inline-flex items-center gap-2 border border-border px-6 py-3.5 rounded-2xl font-semibold hover:bg-stone-100 transition-colors"
        >
          {t("goDashboardCta")}
        </Link>
      </div>
    </div>
  );
}
