using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Graph;

namespace LostFound.AI.Importers
{
    /// <summary>
    /// The one real, end-to-end-wired <see cref="IDatasetImporter"/> for
    /// Phase 2A Part 4: a hand-curated, statically embedded seed set
    /// covering the Lost &amp; Found domain list from Phase 2A Part 3's own
    /// spec (Phones, Wallets, Bags, Keys, Passports, IDs, Watches, Laptops,
    /// Tablets, Chargers, Power Banks, Earbuds, Jewelry, Clothing, Shoes,
    /// Glasses, Documents, Medical devices, Pets) in Arabic/English/Urdu,
    /// including the exact Phone/Bag/Wallet ontology examples the Part 3
    /// spec itself gives.
    /// </summary>
    /// <remarks>
    /// The other 8 sources the Part 4 spec lists (ConceptNet, Open
    /// Multilingual WordNet, Arabic WordNet, Wikidata, DBpedia, open
    /// multilingual synonym datasets, morphological dictionaries,
    /// transliteration/spell-correction datasets) are NOT implemented here -
    /// bulk-importing any of them (Wikidata's dump alone is 100+ GB;
    /// ConceptNet's full CSV is hundreds of MB) isn't something to attempt
    /// in one session even where the host is reachable, and Arabic
    /// WordNet/OMW's licensing is still unresolved per the Phase 2A
    /// pre-implementation plan. <see cref="IDatasetImporter"/> is the seam:
    /// adding any of them later is "implement this interface", nothing
    /// about <see cref="IImportCoordinator"/> or downstream pipeline stages
    /// changes.
    /// </remarks>
    internal sealed class LostFoundSeedDataImporter : IDatasetImporter
    {
        public string DatasetName => "lostfound-seed";

        // Bump whenever the seed data below changes - this IS the dataset's
        // real version, exactly as meaningful as a Wikidata dump date would
        // be for a static, hand-curated fixture.
        //
        // 1.1.0 (PHASE-VALIDATION-07): added Brand/Color/Material concepts.
        // Root cause of the Brand/Color classification-and-ranking defect
        // this version fixes: the ontology contained ONLY object-type
        // concepts (Phone, Bag, Charger, ...) - RawConceptRecord/Canonicalizer
        // never populated Concept.Brands/Colors/Materials for ANY concept,
        // so EntityType.Brand/Color/Material could never be recognized no
        // matter how a query was phrased. These new entries use the SAME
        // Entry() mechanism as every object-type concept above, tagged via
        // Category = "Brand"/"Color"/"Material" - EntityRecognizer (see its
        // own PHASE-VALIDATION-07 remarks) treats any concept carrying one
        // of those category tags as a recognizable value of that attribute,
        // generically. This is a genuine but intentionally modest starting
        // set (real brands/colors/materials relevant to the Lost & Found
        // domain, not an exhaustive catalog) - growing it further is purely
        // a data change (more Entry(...) rows here, or a dedicated
        // IDatasetImporter later), never a code change.
        //
        // 1.2.0 (PHASE-VALIDATION-07 generalization testing): added feminine
        // Arabic adjective aliases to every color concept (e.g. "زرقاء"
        // alongside "أزرق") - found by testing an unseen report describing a
        // blue Nike watch ("ساعة نايك زرقاء"); Arabic feminine nouns like
        // "ساعة" require the feminine color inflection. A data addition, not
        // a code change - see the Colors section below.
        //
        // 1.3.0 (PHASE-VALIDATION-08): added AirPods/AirPods Pro/Galaxy Buds
        // as Earbuds aliases (see that Entry() call). The rest of Problem
        // 2's missing vocabulary (Camera, Canon, Nikon, GoPro, ...) is
        // deliberately NOT added here - see JsonLexiconDatasetImporter and
        // WikidataDatasetImporter, the new data-driven/live sources this
        // phase introduces instead of growing this hand-written fixture
        // further.
        //
        // 1.4.0 (Arabic-Classification-Validation fixes): added Arabic
        // "سماعة"/"سماعه"/"ايربودز" aliases to Earbuds - see that Entry()
        // call for the real, reproduced case this closes.
        private const string DatasetVersion = "1.5.1";

        private sealed record SeedEntry(
            string Key,
            IReadOnlyDictionary<string, string> Names,
            IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases,
            string Category,
            string? ParentKey);

        public Task<DatasetSnapshot> FetchAsync(CancellationToken cancellationToken = default)
        {
            var entries = BuildSeedEntries();
            var concepts = new List<RawConceptRecord>();
            var relationships = new List<RawRelationshipRecord>();
            var englishNameByKey = entries.ToDictionary(e => e.Key, e => e.Names["en"]);

            foreach (var entry in entries)
            {
                foreach (var (language, name) in entry.Names)
                {
                    var aliases = entry.Aliases.TryGetValue(language, out var languageAliases)
                        ? languageAliases
                        : Array.Empty<string>();

                    concepts.Add(new RawConceptRecord(
                        SourceDataset: DatasetName,
                        SourceId: $"seed:{entry.Key}",
                        CanonicalName: name,
                        LanguageCode: language,
                        Synonyms: Array.Empty<string>(),
                        Aliases: aliases,
                        DialectWords: Array.Empty<string>(),
                        CommonMisspellings: Array.Empty<string>(),
                        Categories: new[] { entry.Category },
                        ParentNames: entry.ParentKey != null ? new[] { englishNameByKey[entry.ParentKey] } : Array.Empty<string>()));
                }

                if (entry.ParentKey != null)
                {
                    relationships.Add(new RawRelationshipRecord(
                        DatasetName, entry.Names["en"], englishNameByKey[entry.ParentKey], RelationshipType.IsA));
                }
            }

            return Task.FromResult(new DatasetSnapshot(DatasetName, DatasetVersion, DateTime.UtcNow, concepts, relationships));
        }

        private static IReadOnlyList<SeedEntry> BuildSeedEntries()
        {
            var entries = new List<SeedEntry>
            {
                // TRIED AND REVERTED (Multi-Representation Embedding
                // validation, iteration 3): adding "جوال" as an alias here
                // fixed the specific query-side resolution gap it was meant
                // to fix (a real Arabic query using this extremely common
                // Gulf/Saudi word for "mobile phone" now resolves an Object
                // entity where it previously resolved none), but measurably
                // REGRESSED the full 60-query benchmark in aggregate (Top-1
                // 68.33%->65%, MRR 0.7816->0.7568, NDCG@5 80.51->77.49,
                // Recall@5 90%->86.67%) rather than improving it. Root cause,
                // confirmed via live RankExplain: "جوال" was added to this
                // PARENT concept ("phone"), but real candidates are
                // classified as the CHILD concept ("smartphone", IsA phone).
                // A query resolving to the parent while the true-match
                // candidate resolves to the child is judged
                // ObjectTypeCompatibility.RelatedCluster (a -15 penalty), not
                // .Same (no penalty) - i.e. teaching the query to recognize
                // "جوال" as a phone made it MORE likely to penalize the
                // exact-matching smartphone candidate as merely "related"
                // rather than "the same", which outweighed the benefit of
                // resolving the query at all. Reverted per this project's
                // standing rule: keep a change only when it objectively
                // improves the benchmark, not just the one case it targeted.
                // A better future fix would address the ROOT issue (direct
                // IsA parent/child pairs being penalized at the same tier as
                // arbitrary graph-neighbor "RelatedCluster" matches) rather
                // than the alias gap alone - not attempted here for lack of
                // benchmark evidence isolating that specific change's effect.
                Entry("phone", "Phone", "هاتف", "فون", "Electronics", null,
                    enAliases: new[] { "mobile", "cell phone", "handset" }, arAliases: new[] { "موبايل", "تليفون" }, urAliases: new[] { "موبائل" }),
                Entry("smartphone", "Smartphone", "هاتف ذكي", "اسمارٹ فون", "Electronics", "phone"),
                Entry("android-phone", "Android Phone", "هاتف أندرويد", "اینڈرائیڈ فون", "Electronics", "smartphone"),

                Entry("bag", "Bag", "حقيبة", "بیگ", "Bags", null, arAliases: new[] { "شنطة", "شنطه" }),
                Entry("backpack", "Backpack", "حقيبة ظهر", "بستہ", "Bags", "bag"),
                Entry("handbag", "Handbag", "حقيبة يد", "ہینڈ بیگ", "Bags", "bag"),
                Entry("suitcase", "Suitcase", "حقيبة سفر", "سوٹ کیس", "Bags", "bag"),

                Entry("wallet", "Wallet", "محفظة", "بٹوہ", "Accessories", null, enAliases: new[] { "purse" }, arAliases: new[] { "جزدان" }),
                Entry("leather-wallet", "Leather Wallet", "محفظة جلدية", "چمڑے کا بٹوہ", "Accessories", "wallet"),
                Entry("card-holder", "Card Holder", "حافظة بطاقات", "کارڈ ہولڈر", "Accessories", "wallet"),

                Entry("keys", "Keys", "مفاتيح", "چابیاں", "Accessories", null),
                Entry("passport", "Passport", "جواز سفر", "پاسپورٹ", "Documents", null),
                Entry("id-card", "ID Card", "بطاقة هوية", "شناختی کارڈ", "Documents", null,
                    enAliases: new[] { "identity card", "national id" }),
                Entry("watch", "Watch", "ساعة", "گھڑی", "Accessories", null),
                Entry("laptop", "Laptop", "حاسوب محمول", "لیپ ٹاپ", "Electronics", null, arAliases: new[] { "لابتوب" }),
                Entry("tablet", "Tablet", "جهاز لوحي", "ٹیبلٹ", "Electronics", null),
                Entry("charger", "Charger", "شاحن", "چارجر", "Electronics", null),
                Entry("power-bank", "Power Bank", "بطارية محمولة", "پاور بینک", "Electronics", null, arAliases: new[] { "باور بانك" }),
                Entry("earbuds", "Earbuds", "سماعات أذن", "ایئربڈز", "Electronics", null,
                    // PHASE-VALIDATION-08: AirPods/AirPods Pro and Galaxy
                    // Buds are real, commonly-lost consumer products that
                    // ARE earbuds (not a distinct object type) - enriching
                    // the existing concept with aliases, not creating a
                    // second "Earbuds" concept, avoids the exact
                    // cross-dataset duplication risk documented in the
                    // Enterprise Lexical Architecture design (Problem 2).
                    enAliases: new[] { "earphones", "headphones", "airpods", "airpods pro", "galaxy buds" },
                    // Arabic-Classification-Validation.md report, item #2:
                    // "فقدت سماعة ايربودز بيضاء داخل حقيبة صغيرة" was
                    // misclassified as ObjectType="حقيبه" (bag - the
                    // CONTAINER the earbuds were inside, not the earbuds
                    // themselves). Root cause was NOT a "container vs.
                    // contained item" ranking/selection defect - "سماعة"
                    // (singular "earpiece/headphone", the word actually
                    // used) and "ايربودز" (the Arabic transliteration of
                    // "AirPods") were both simply absent from this
                    // concept's Arabic vocabulary (only the formal plural
                    // "سماعات أذن" and English "airpods" existed), so
                    // EntityRecognizer never found the real item as a
                    // genuine entity at all, leaving the container mention
                    // as the only genuine Object entity to fall back to.
                    // Adding the actually-used words directly fixes the
                    // observed case: with "سماعة"/"ايربودز" now genuinely
                    // recognized, they win as the FIRST Object entity
                    // (GetGenuineEntity's existing FirstOrDefault), before
                    // "حقيبة" is ever reached - no new selection/ordering
                    // logic required. This is intentionally a narrow,
                    // data-only fix for the observed gap, not a general
                    // architectural solution to container-vs-contained-item
                    // ambiguity as a class of problem (see the
                    // implementation report's Remaining Limitations).
                    arAliases: new[] { "سماعة", "سماعه", "ايربودز" }),
                Entry("jewelry", "Jewelry", "مجوهرات", "زیورات", "Accessories", null),
                Entry("clothing", "Clothing", "ملابس", "کپڑے", "Apparel", null),
                Entry("shoes", "Shoes", "حذاء", "جوتے", "Apparel", null),
                Entry("glasses", "Glasses", "نظارة", "عینک", "Accessories", null,
                    enAliases: new[] { "eyeglasses", "spectacles" }),
                Entry("document", "Document", "مستند", "دستاویز", "Documents", null),
                Entry("medical-device", "Medical Device", "جهاز طبي", "طبی آلہ", "Medical", null),
                Entry("pet", "Pet", "حيوان أليف", "پالتو جانور", "Animals", null),

                // ---- Brands (PHASE-VALIDATION-07) ----
                // Category = "Brand" is the role tag EntityRecognizer keys
                // on - see this class's DatasetVersion remarks. No parent
                // (brands are not "a kind of" some other concept here).
                Entry("brand-samsung", "Samsung", "سامسونج", "سیمسنگ", "Brand", null,
                    arAliases: new[] { "سامسونق" }),
                Entry("brand-apple", "Apple", "ابل", "ایپل", "Brand", null,
                    arAliases: new[] { "آبل", "أبل" }),
                Entry("brand-sony", "Sony", "سوني", "سونی", "Brand", null),
                Entry("brand-xiaomi", "Xiaomi", "شاومي", "شیاؤمی", "Brand", null),
                Entry("brand-huawei", "Huawei", "هواوي", "ہواوے", "Brand", null),
                Entry("brand-nokia", "Nokia", "نوكيا", "نوکیا", "Brand", null),
                Entry("brand-lg", "LG", "إل جي", "ایل جی", "Brand", null),
                Entry("brand-dell", "Dell", "ديل", "ڈیل", "Brand", null),
                Entry("brand-hp", "HP", "اتش بي", "ایچ پی", "Brand", null),
                Entry("brand-lenovo", "Lenovo", "لينوفو", "لینووو", "Brand", null),
                Entry("brand-anker", "Anker", "انكر", "اینکر", "Brand", null),
                Entry("brand-nike", "Nike", "نايك", "نائیکی", "Brand", null),
                Entry("brand-adidas", "Adidas", "اديداس", "ایڈیڈاس", "Brand", null),
                Entry("brand-puma", "Puma", "بوما", "پوما", "Brand", null),
                Entry("brand-gucci", "Gucci", "غوتشي", "گوچی", "Brand", null),

                // ---- Colors (PHASE-VALIDATION-07) ----
                // Canonical Arabic spellings use the correct hamza forms
                // (e.g. "أبيض") deliberately, rather than the pre-collapsed
                // "ابيض" - EntityRecognizer/DictionarySpellCorrectionService
                // now normalize consistently with runtime query text (see
                // their own PHASE-VALIDATION-07 remarks), so recognition
                // does not depend on pre-normalizing the ontology data.
                //
                // arAliases carry the feminine Arabic adjective form (e.g.
                // "زرقاء" for "blue") alongside the masculine canonical name
                // ("أزرق") - discovered via the Required Verification
                // generalization test ("ساعة نايك زرقاء", a blue Nike watch:
                // "ساعة"/watch is grammatically feminine, so Arabic requires
                // the feminine color inflection). This is ontology-data
                // completeness, not a code change: any concept can already
                // carry an Aliases entry per language, EntityRecognizer
                // already indexes every alias generically (see its own
                // remarks) - the fix is adding the missing lexical form, the
                // exact "expand the lexical resource" outcome PHASE-
                // VALIDATION-07's Stage 10 calls for.
                Entry("color-white", "White", "أبيض", "سفید", "Color", null, arAliases: new[] { "بيضاء" }),
                Entry("color-black", "Black", "أسود", "کالا", "Color", null, arAliases: new[] { "سوداء" }),
                Entry("color-red", "Red", "أحمر", "سرخ", "Color", null, arAliases: new[] { "حمراء" }),
                Entry("color-blue", "Blue", "أزرق", "نیلا", "Color", null, arAliases: new[] { "زرقاء" }),
                Entry("color-green", "Green", "أخضر", "سبز", "Color", null, arAliases: new[] { "خضراء" }),
                Entry("color-yellow", "Yellow", "أصفر", "پیلا", "Color", null, arAliases: new[] { "صفراء" }),
                Entry("color-gray", "Gray", "رمادي", "سرمئی", "Color", null,
                    enAliases: new[] { "grey" }, arAliases: new[] { "رمادية" }),
                Entry("color-silver", "Silver", "فضي", "چاندی جیسا", "Color", null, arAliases: new[] { "فضية" }),
                Entry("color-gold", "Gold", "ذهبي", "سنہری", "Color", null,
                    enAliases: new[] { "golden" }, arAliases: new[] { "ذهبية" }),
                Entry("color-brown", "Brown", "بني", "بھورا", "Color", null, arAliases: new[] { "بنية" }),
                Entry("color-pink", "Pink", "وردي", "گلابی", "Color", null, arAliases: new[] { "وردية" }),
                Entry("color-purple", "Purple", "بنفسجي", "جامنی", "Color", null, arAliases: new[] { "بنفسجية" }),
                Entry("color-orange", "Orange", "برتقالي", "نارنجی", "Color", null, arAliases: new[] { "برتقالية" }),
                Entry("color-navy", "Navy Blue", "كحلي", "گہرا نیلا", "Color", null, arAliases: new[] { "كحلية" }),
                Entry("color-beige", "Beige", "بيج", "بیج", "Color", null),

                // ---- Materials (PHASE-VALIDATION-07) ----
                Entry("material-leather", "Leather", "جلد", "چمڑا", "Material", null),
                Entry("material-metal", "Metal", "معدن", "دھات", "Material", null),
                Entry("material-plastic", "Plastic", "بلاستيك", "پلاسٹک", "Material", null),
                Entry("material-fabric", "Fabric", "قماش", "کپڑا", "Material", null,
                    enAliases: new[] { "cloth" }),
                Entry("material-wood", "Wood", "خشب", "لکڑی", "Material", null),
                Entry("material-glass", "Glass", "زجاج", "شیشہ", "Material", null),
                Entry("material-silicone", "Silicone", "سيليكون", "سلیکون", "Material", null),
                Entry("material-cotton", "Cotton", "قطن", "روئی", "Material", null)
            };

            return entries;
        }

        private static SeedEntry Entry(
            string key, string en, string ar, string ur, string category, string? parentKey,
            IReadOnlyList<string>? enAliases = null, IReadOnlyList<string>? arAliases = null, IReadOnlyList<string>? urAliases = null) =>
            new(
                key,
                new Dictionary<string, string> { ["en"] = en, ["ar"] = ar, ["ur"] = ur },
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["en"] = enAliases ?? Array.Empty<string>(),
                    ["ar"] = arAliases ?? Array.Empty<string>(),
                    ["ur"] = urAliases ?? Array.Empty<string>()
                },
                category,
                parentKey);
    }
}
