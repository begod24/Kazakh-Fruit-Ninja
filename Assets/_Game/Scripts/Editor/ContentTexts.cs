using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Texts and achievements kept on the content assets: the dishes' stories in Kazakh and English (the Russian ones
    /// come with the placeholder content), the blades' English names, and the achievement that opens each blade.
    /// Empty texts are filled in; texts already set (e.g. edited by hand) are kept. Achievements always follow the table.
    /// </summary>
    static class ContentTexts
    {
        const string FoodFolder = "Assets/_Game/Data/Foods";
        const string BladeFolder = "Assets/_Game/Data/Collection";

        /// <summary>id → (Kazakh, English) story.</summary>
        static readonly Dictionary<string, (string kk, string en)> Facts = new()
        {
            ["shuzhuk"] = ("Жылқы етінен сарымсақ пен дәмдеуіш қосып жасайтын үй шұжығы; оны қыста, соғым кезінде әзірлейді.",
                "Homemade horse-meat sausage with garlic and spices, made in winter, in the soğym season."),
            ["kazy"] = ("Жылқының қабырға етін майымен бірге ішекке салып жасайды — дастарханның ең құрметті асы.",
                "Horse rib meat with its fat, packed into a casing — the most honoured delicacy of the dastarkhan."),
            ["kurt"] = ("Тұздалған қатықтан кептірілген домалақ құрт. Айлап сақталады — оны алыс сапарға алып шыққан.",
                "Dried balls of salted sour milk. They keep for months, so travellers took them on long journeys."),
            ["baursak"] = ("Ашытқы қамырдан майға пісірілген бауырсақ. Бір табақ бауырсақсыз ешбір той өтпейді.",
                "Puffs of yeast dough fried in oil. No toi feast goes by without a heap of baursaks."),
            ["aport"] = ("Алматының атақты алмасы. Қазақстан — алманың отаны: жабайы Сиверс алмасы әлі күнге Алатауда өседі.",
                "Almaty's famous apple. Kazakhstan is the homeland of apples: wild Sievers apple trees still grow in the Alatau."),
            ["shelpek"] = ("Майға пісірілген жұқа нан. Дәстүр бойынша оны жұма күндері және еске алу күндері пісіреді.",
                "Thin flatbreads fried in oil. By tradition they are made on Fridays and on days of remembrance."),
            ["samsa"] = ("Туралған ет пен пияз салынған үшбұрышты самса; ең дәмдісі тандырда піседі.",
                "Triangular pastries with chopped meat and onion; the tastiest are baked in a tandyr oven."),
            ["irimshik"] = ("Сүтті отта ұзақ қайнатып жасайтын, тәттілеу келетін кептірілген ірімшік.",
                "A sweetish dried curd cheese made from milk simmered slowly over the fire."),
            ["zhent"] = ("Қуырылған тарыны түйіп, сары май, қант және мейізбен араластырып жасайтын тәттілік.",
                "A sweet of crushed roasted millet (tary) mixed with butter, sugar and raisins."),
            ["karta"] = ("Жылқының тоқ ішегі, майын сыртына айналдырып пісіреді; етке қазымен бірге салынады.",
                "Horse intestine turned fat side out and boiled; it is served with beshbarmak alongside kazy."),
            ["golden_aport"] = ("Ілулі тұрғанда кесе бер: әр соққы — бір ұпай, соңғысы оны екіге бөледі.",
                "Keep striking while it hangs: every hit scores a point, and the last one splits it in two."),
            ["kymyz"] = ("Бие сүтінен ашытылған сусын; оны теріден тігілген сабада пісіп дайындайды.",
                "A fermented drink of mare's milk, soured and churned in a leather saba."),
            ["toy"] = ("Той — мереке: қонақтардың басына бақыт тілеп шашу — кәмпит пен тиын шашады.",
                "A toi is a celebration: guests are showered with shashu — sweets and coins for good luck."),
            ["nauryz"] = ("Наурызда — 22 наурыздағы көктемгі жаңа жылда — жеті түрлі дәмнен пісірілетін мерекелік көже.",
                "A festive soup of seven ingredients, cooked for Nauryz, the spring New Year on 22 March."),
            ["bomb_pepper"] = ("Бомба! Кеспе — күйіп қаласың.", "A bomb! Don't cut it — it burns."),
        };

        /// <summary>Blade id → English name and the achievement that opens it, in display order.</summary>
        static readonly (string id, string en, UnlockRequirement unlock)[] Blades =
        {
            ("pyshak", "Knife", new UnlockRequirement(UnlockKind.Free, 0)),
            ("kylysh", "Sabre", new UnlockRequirement(UnlockKind.ClassicScore, 100)),
            ("tulpar", "Tulpar", new UnlockRequirement(UnlockKind.BestCombo, 5)),
            ("shashu", "Shashu", new UnlockRequirement(UnlockKind.ArcadeScore, 200)),
            ("oyu", "Ornament", new UnlockRequirement(UnlockKind.TotalSlices, 1000)),
            // Seven in one stroke: at most ten foods are ever in the air, so eight would hardly ever be possible.
            ("naizagai", "Lightning", new UnlockRequirement(UnlockKind.BestCombo, 7)),
            ("altyn_semser", "Golden Sword", new UnlockRequirement(UnlockKind.CardsCollected, 14)),
        };

        public static UnlockRequirement Unlock(string bladeId)
        {
            foreach ((string id, string _, UnlockRequirement unlock) in Blades)
                if (id == bladeId) return unlock;
            return new UnlockRequirement(UnlockKind.Free, 0);
        }

        public static void Apply()
        {
            foreach (KeyValuePair<string, (string kk, string en)> fact in Facts)
            {
                var food = AssetDatabase.LoadAssetAtPath<FoodDefinition>($"{FoodFolder}/Food_{fact.Key}.asset");
                if (food == null) continue;
                bool changed = false;
                if (string.IsNullOrEmpty(food.factKk)) { food.factKk = fact.Value.kk; changed = true; }
                if (string.IsNullOrEmpty(food.factEn)) { food.factEn = fact.Value.en; changed = true; }
                if (changed) EditorUtility.SetDirty(food);
            }

            foreach ((string id, string en, UnlockRequirement unlock) in Blades)
            {
                var blade = AssetDatabase.LoadAssetAtPath<BladeDefinition>($"{BladeFolder}/Blade_{id}.asset");
                if (blade == null) continue;
                if (string.IsNullOrEmpty(blade.nameEn)) blade.nameEn = en;
                blade.unlock = unlock;
                EditorUtility.SetDirty(blade);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
