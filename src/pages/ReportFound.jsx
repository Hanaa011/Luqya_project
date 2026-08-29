import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  Upload,
  MapPin,
  Calendar,
  HeartHandshake,
  Gift,
  Users,
  ArrowUpRight,
  AlertCircle,
} from "lucide-react";

import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import DammaMark from "../components/DammaMark";
import GuestContactFields from "../components/GuestContactFields";
import { createReport, uploadReportImage } from "../api/reports";
import { listLocations, createLocation } from "../api/locations";
import { imageFileToBase64 } from "../api/search";
import { ReportType, PreferredContactType } from "../api/enums";
import { ApiError } from "../api/httpClient";
import {
  isValidSaudiMobile,
  normalizeSaudiMobile,
  normalizeSaudiPhoneInput,
} from "../lib/saudiPhone";
import { isValidEmail } from "../lib/email";
import { setKnownReporterId } from "../api/reporterIdentity";
import { buildReporterFields } from "../lib/reporterFields";
import { validateImageFile, ImageValidationReason } from "../lib/imageValidation";

// Task B: same message set as ReportLost.jsx (kept as a local copy, not a
// shared component, to match this file's existing self-contained style —
// see Field below, which is likewise duplicated rather than imported).
function imageValidationMessage(tr, reason) {
  switch (reason) {
    case ImageValidationReason.TOO_LARGE:
      return tr({
        ar: "حجم الصورة كبير جدًا (الحد الأقصى 8 ميجابايت).",
        en: "That photo is too large (8 MB maximum).",
        ur: "یہ تصویر بہت بڑی ہے (زیادہ سے زیادہ 8 MB)۔",
      });
    case ImageValidationReason.INVALID_FORMAT:
      return tr({
        ar: "صيغة الصورة غير مدعومة. الرجاء استخدام JPEG أو PNG أو WEBP.",
        en: "That file isn't a supported image. Please use JPEG, PNG, or WEBP.",
        ur: "یہ فائل معاون تصویر نہیں ہے۔ براہ کرم JPEG، PNG، یا WEBP استعمال کریں۔",
      });
    default:
      return tr({
        ar: "تعذّر قراءة هذه الصورة. جرّب صورة أخرى.",
        en: "Couldn't read that photo. Please try a different file.",
        ur: "یہ تصویر پڑھی نہیں جا سکی۔ دوسری فائل آزمائیں۔",
      });
  }
}

export default function ReportFound() {
  const { t, tr, lang, dir } = useI18n();
  const { profile } = useAuth();

  const [phase, setPhase] = useState("form"); // form | saving | thanks
  const [description, setDescription] = useState("");
  const [locationText, setLocationText] = useState("");
  const [lostFoundDate, setLostFoundDate] = useState("");
  const [imageFile, setImageFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [imageError, setImageError] = useState(null);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [errorMsg, setErrorMsg] = useState(null);
  const [knownLocations, setKnownLocations] = useState([]);
  const [guest, setGuest] = useState({
    reporterName: "",
    reporterPhone: "",
    reporterEmail: "",
    preferredContact: PreferredContactType.PHONE,
  });
  const [phoneError, setPhoneError] = useState(false);
  const [emailError, setEmailError] = useState(false);
  const [profilePhone, setProfilePhone] = useState("");
  const [profilePhoneError, setProfilePhoneError] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    document.title = tr({
      ar: "الإبلاغ عن غرض تم العثور عليه — لُقيا",
      en: "Report a found item — Luqya",
      ur: "ملی ہوئی چیز کی رپورٹ — لقیا",
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

  // Task B: same content-inspection validation as ReportLost.jsx, and — new
  // in this file — the picked File is now actually kept (imageFile), not
  // just its local preview URL, since it needs to be uploaded on submit.
  async function handleFile(file) {
    if (!file) {
      if (preview) URL.revokeObjectURL(preview);
      setImageFile(null);
      setPreview(null);
      setImageError(null);
      return;
    }

    const reason = await validateImageFile(file);
    if (reason) {
      setImageError(imageValidationMessage(tr, reason));
      return;
    }

    if (preview) URL.revokeObjectURL(preview);
    setImageError(null);
    setImageFile(file);
    setPreview(URL.createObjectURL(file));
  }

  function updateGuest(field, value) {
    const nextValue =
      field === "reporterPhone" ? normalizeSaudiPhoneInput(value) : value;

    setGuest((g) => ({ ...g, [field]: nextValue }));
    if (field === "reporterPhone") setPhoneError(false);
    if (field === "reporterEmail") setEmailError(false);
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

    if (!profile && !isValidEmail(guest.reporterEmail)) {
      setEmailError(true);
      return;
    }

    const profileHasPhone = Boolean((profile?.phoneNumber || "").trim());
    if (profile && !profileHasPhone && !isValidSaudiMobile(profilePhone)) {
      setProfilePhoneError(true);
      return;
    }

    setSubmitting(true);
    setPhase("saving");

    try {
      const locationId = await resolveLocationId(locationText || "—");

      // Task B: previously this form never attached the picked image to the
      // report at all — it was local-preview-only (Luqya-System-Reference.md
      // §9/§38 High #7). Now uploaded to blob storage first, then persisted
      // via CreateReportDto.imagePath, exactly like ReportLost.jsx.
      let imagePath;
      if (imageFile) {
        const imageBase64 = await imageFileToBase64(imageFile);
        setUploadingImage(true);
        try {
          imagePath = await uploadReportImage(imageBase64);
        } catch (uploadErr) {
          uploadErr.isImageUpload = true;
          throw uploadErr;
        } finally {
          setUploadingImage(false);
        }
      }

      const report = await createReport({
        locationId,
        locationDetails: locationText,
        type: ReportType.FOUND,
        description,
        lostFoundDate: lostFoundDate ? new Date(lostFoundDate).toISOString() : undefined,
        imagePath,
        isItemWithFinder: true,
        pickupLocation: locationText,
        ...buildReporterFields({
          profile: profile
            ? {
                ...profile,
                phoneNumber: normalizeSaudiMobile(
                  profileHasPhone ? profile.phoneNumber : profilePhone
                ),
              }
            : profile,
          guest: !profile
            ? {
                ...guest,
                reporterPhone: normalizeSaudiMobile(guest.reporterPhone),
              }
            : guest,
        }),
      });

      if (report?.reporterId) {
        setKnownReporterId(report.reporterId);
      }

      setPhase("thanks");
    } catch (err) {
      setPhase("form");

      if (err instanceof ApiError && err.isUnauthorized) {
        setErrorMsg(
          tr({
            ar: "انتهت جلستك. سجّل الدخول مرة أخرى.",
            en: "Your session expired. Please log in again.",
            ur: "آپ کا سیشن ختم ہو گیا۔ دوبارہ لاگ ان کریں۔",
          })
        );
      } else if (err instanceof ApiError && err.code === "LostFound:Reporter:0004") {
        setErrorMsg(
          tr({
            ar: "رقم الجوال هذا مسجل مسبقًا بحساب آخر.",
            en: "This phone number is already registered to another account.",
            ur: "یہ فون نمبر پہلے سے کسی دوسرے اکاؤنٹ سے رجسٹرڈ ہے۔",
          })
        );
      } else if (err.isImageUpload) {
        setErrorMsg(
          tr({
            ar: "تعذّر رفع الصورة. تحقق من الصورة وحاول الإرسال مرة أخرى.",
            en: "Couldn't upload the photo. Please check it and try submitting again.",
            ur: "تصویر اپ لوڈ نہیں ہو سکی۔ اسے چیک کریں اور دوبارہ جمع کروائیں۔",
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
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="py-16 lg:py-24">
      <div className="max-w-4xl mx-auto px-6">
        {phase === "form" && (
          <div className="animate-rise-in">
            <Link
              to="/report"
              className="
                mb-8 inline-flex items-center gap-1.5
                text-sm font-semibold text-muted-foreground/60
                transition-colors duration-200
                hover:text-primary
                focus-visible:outline-none
                focus-visible:text-primary
                focus-visible:underline
              "
            >
              <ArrowUpRight
                className={`size-3 ${dir === "rtl" ? "" : "-scale-x-100"}`}
                strokeWidth={1.6}
                aria-hidden="true"
              />
              {t("backLabel")}
            </Link>

            <div className="mb-10">
              <div className="inline-flex items-center gap-2 text-[11px] font-mono uppercase tracking-widest text-accent font-bold mb-3">
                <HeartHandshake className="size-3.5" />
                {t("foundEyebrow")}
              </div>
              <h1 className="font-display text-4xl lg:text-5xl font-extrabold tracking-tight mb-3">
                {t("foundTitle")}
              </h1>
              <p className="text-muted-foreground text-lg">{t("foundSub")}</p>
            </div>

            {errorMsg && (
              <div className="mb-6 flex items-start gap-2.5 rounded-2xl bg-error-tint text-error px-4 py-3 text-sm">
                <AlertCircle className="size-4 shrink-0 mt-0.5" />
                <span>{errorMsg}</span>
              </div>
            )}

            <form
              onSubmit={handleSubmit}
              className="bg-card border border-accent/20 rounded-[2rem] p-8 lg:p-12 shadow-soft space-y-8 relative overflow-hidden"
            >
              <div className="absolute -top-20 -end-20 size-64 rounded-full bg-accent/10 blur-3xl" />

              <div className="relative space-y-8">
                <Field label={t("fldDesc")}>
                  <textarea
                    required
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder={t("fldDescPh")}
                    rows={5}
                    className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/15 transition-all resize-none"
                  />
                </Field>

                <div className="grid md:grid-cols-2 gap-5">
                  <Field label={t("fldLocation")} icon={MapPin}>
                    <input
                      type="text"
                      required
                      value={locationText}
                      onChange={(e) => setLocationText(e.target.value)}
                      placeholder={t("fldLocationPh")}
                      className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/15 transition-all"
                    />
                  </Field>

                  <Field label={t("fldDate")} icon={Calendar}>
                    <input
                      type="date"
                      value={lostFoundDate}
                      onChange={(e) => setLostFoundDate(e.target.value)}
                      className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/15 transition-all"
                    />
                  </Field>
                </div>

                <Field
          label={tr({
            ar: "أضف صورة (اختياري)",
            en: "Add a photo (optional)",
            ur: "تصویر شامل کریں (اختیاری)",
          })}
        >
                  <label className="block relative rounded-2xl border-2 border-dashed border-stone-200 hover:border-accent/50 hover:bg-accent/[0.04] transition-colors cursor-pointer overflow-hidden">
                    {preview ? (
                      <img src={preview} alt="" className="w-full h-64 object-cover" />
                    ) : (
                      <div className="py-16 flex flex-col items-center gap-3">
                        <div className="size-14 rounded-2xl bg-accent/10 text-accent grid place-items-center">
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
                  {preview && (
                    <button
                      type="button"
                      onClick={() => handleFile(null)}
                      className="mt-2 text-xs font-semibold text-muted-foreground hover:text-error transition-colors"
                    >
                      {t("removePhotoCta")}
                    </button>
                  )}
                  {imageError && <p className="text-xs text-error mt-2">{imageError}</p>}
                </Field>

                {!profile && (
                  <GuestContactFields
                    t={t}
                    tone="accent"
                    values={guest}
                    onChange={updateGuest}
                    phoneError={phoneError}
                    emailError={emailError}
                  />
                )}

                {profile && !(profile?.phoneNumber || "").trim() && (
                  <Field label={t("fldMobile")}>
                    <input
                      type="tel"
                      inputMode="tel"
                      dir="ltr"
                      value={profilePhone}
                      onChange={(e) => {
                        setProfilePhone(normalizeSaudiPhoneInput(e.target.value));
                        setProfilePhoneError(false);
                      }}
                      placeholder={t("fldMobilePh")}
                      className="w-full px-5 py-4 rounded-2xl bg-stone-50 border border-stone-200 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/15 transition-all text-start"
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
                    className="inline-flex items-center gap-2 bg-accent text-accent-foreground px-8 py-4 rounded-2xl font-semibold shadow-luxe hover:-translate-y-0.5 transition-transform disabled:opacity-70 disabled:translate-y-0"
                  >
                    <Gift className="size-4" />
                    {t("foundSubmitCta")}
                  </button>
                </div>
              </div>
            </form>
          </div>
        )}

        {phase === "saving" && (
          <div className="max-w-md mx-auto text-center py-20 animate-rise-in">
            <div className="relative size-20 mx-auto mb-8">
              <div className="absolute inset-0 rounded-full bg-accent/20 animate-glow-pulse" />
              <div className="relative size-20 rounded-full bg-accent text-accent-foreground grid place-items-center shadow-luxe">
                <Gift className="size-8" strokeWidth={1.5} />
              </div>
            </div>
            <p className="font-display text-xl font-bold">
              {uploadingImage
                ? tr({ ar: "جارٍ رفع الصورة…", en: "Uploading photo…", ur: "تصویر اپ لوڈ ہو رہی ہے…" })
                : t("foundSaving")}
            </p>
          </div>
        )}

        {phase === "thanks" && (
          <div className="max-w-xl mx-auto text-center py-12 animate-rise-in">
            <div className="relative size-24 mx-auto mb-8">
              <div className="absolute inset-0 rounded-full bg-accent/15 animate-ping-slow" />
              <div className="relative size-24 rounded-full bg-accent text-accent-foreground grid place-items-center shadow-luxe">
                <HeartHandshake className="size-10" strokeWidth={1.5} />
              </div>
            </div>

            <div className="text-[11px] font-mono uppercase tracking-widest text-accent font-bold mb-3">
              {t("thanksLabel")}
            </div>

            <h2 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight mb-4">
              {t("thanksTitle")}
            </h2>

            <p className="text-muted-foreground leading-relaxed mb-10 max-w-md mx-auto">
              {t("thanksBody")}
            </p>

            <div className="inline-flex items-center gap-3 rounded-2xl border border-accent/20 bg-accent/5 px-6 py-4 mb-10">
              <Users className="size-5 text-accent" />
              <span className="text-sm font-semibold">{t("reunitedCounter")}</span>
            </div>

            <div className="flex flex-wrap justify-center gap-3">
              <Link
                to="/report/found"
                onClick={() => setPhase("form")}
                className="inline-flex items-center gap-2 bg-accent text-accent-foreground px-6 py-3.5 rounded-2xl font-semibold shadow-luxe hover:-translate-y-0.5 transition-transform"
              >
                <DammaMark className="size-4" />
                {t("reportAnotherCta")}
              </Link>
              <Link
                to="/"
                className="inline-flex items-center gap-2 border border-border px-6 py-3.5 rounded-2xl font-semibold hover:bg-stone-100 transition-colors"
              >
                {t("backHomeCta")}
              </Link>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

function Field({ label, icon: Icon, children }) {
  return (
    <div>
      <label className="text-sm font-semibold flex items-center gap-2 mb-2.5">
        {Icon && <Icon className="size-3.5 text-muted-foreground" />}
        {label}
      </label>
      {children}
    </div>
  );
}
