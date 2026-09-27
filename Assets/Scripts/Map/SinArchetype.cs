namespace ShellGame.Map
{
    // Противники на данном этапе — олицетворения смертных грехов, кроме Уныния:
    // Уныние (Sloth/Acedia) — это главный герой, а не враг.
    public enum SinArchetype
    {
        Pride,      // Гордыня
        Wrath,      // Гнев
        Envy,       // Зависть
        Gluttony,   // Чревоугодие
        Lust,       // Похоть
        Greed       // Жадность / Алчность
    }

    public static class SinArchetypeExtensions
    {
        public static string ToEncounterId(this SinArchetype sin) => sin.ToString();
    }
}