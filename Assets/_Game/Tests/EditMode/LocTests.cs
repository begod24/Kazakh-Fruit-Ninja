using NUnit.Framework;

namespace KazakhNinja.Tests
{
    public class LocTests
    {
        static readonly Phrase Title = new("test_title", "SLICE TO PLAY", "ОЙНАУ ҮШІН КЕС!", "РЕЖЬ, ЧТОБЫ ИГРАТЬ!");

        [TearDown]
        public void Unbind() => Loc.Bind(null);

        [Test]
        public void Default_IsEnglishTitlesWithKazakhUnderThem()
        {
            var save = new SaveData();
            Loc.Bind(save);

            Assert.That(Loc.Language, Is.EqualTo(Language.EnglishKazakh));
            Assert.That(Title.Main, Is.EqualTo("SLICE TO PLAY"));
            Assert.That(Title.Sub, Is.EqualTo("ОЙНАУ ҮШІН КЕС!"));
            Assert.That(Title.Text, Is.EqualTo("ОЙНАУ ҮШІН КЕС!"), "running text is Kazakh by default");
        }

        [Test]
        public void OneLanguage_HasNoSecondLine()
        {
            var save = new SaveData();
            Loc.Bind(save);

            foreach ((Language language, string expected) in new[]
                     { (Language.English, "SLICE TO PLAY"), (Language.Kazakh, "ОЙНАУ ҮШІН КЕС!"), (Language.Russian, "РЕЖЬ, ЧТОБЫ ИГРАТЬ!") })
            {
                Loc.Language = language;
                Assert.That(Title.Main, Is.EqualTo(expected));
                Assert.That(Title.Text, Is.EqualTo(expected));
                Assert.That(Title.Sub, Is.Null);
                Assert.That(save.language, Is.EqualTo((int)language), "the choice is kept in the save");
            }
        }

        [Test]
        public void MissingTranslation_FallsBack()
        {
            Loc.Bind(new SaveData { language = (int)Language.Russian });
            var english = new Phrase("test_en_only", "Only English", "", "");
            Assert.That(english.Text, Is.EqualTo("Only English"));
        }

        [Test]
        public void EveryPhraseHasAllThreeLanguages()
        {
            foreach (Phrase phrase in UiText.All)
            {
                Assert.That(phrase.En, Is.Not.Empty, phrase.Key);
                Assert.That(phrase.Kk, Is.Not.Empty, phrase.Key);
                Assert.That(phrase.Ru, Is.Not.Empty, phrase.Key);
            }
        }
    }
}
