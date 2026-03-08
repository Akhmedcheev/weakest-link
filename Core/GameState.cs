namespace WeakestLink.Core
{
    /// <summary>
    /// Состояния игрового процесса (State Machine).
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Начальное состояние, игра не запущена или находится в режиме ожидания.
        /// </summary>
        Idle,

        /// <summary>Студийное вступление: основная тема, логотип на экране.</summary>
        IntroOpening,

        /// <summary>Вступление: ведущая представляет шоу, список участников.</summary>
        IntroNarrative,

        /// <summary>Представление игроков по именам.</summary>
        PlayerIntro,

        /// <summary>Ведущая объясняет правила. После — автоподготовка раунда 1.</summary>
        RulesExplanation,

        /// <summary>
        /// Игроки на местах, таймер установлен и готов к запуску.
        /// </summary>
        RoundReady,

        /// <summary>
        /// Идет раунд, таймер уменьшается.
        /// </summary>
        Playing,

        /// <summary>
        /// Раунд окончен, показ итогов раунда перед голосованием.
        /// </summary>
        RoundSummary,

        /// <summary>
        /// Голосование за слабое звено (45-секундный таймер).
        /// </summary>
        Voting,

        /// <summary>
        /// Обсуждение результатов голосования перед исключением.
        /// </summary>
        Discussion,

        /// <summary>
        /// Вскрытие голосов (звуковое сопровождение + визуализация).
        /// </summary>
        Reveal,

        /// <summary>
        /// Выбывание игрока, подведение итогов раунда.
        /// </summary>
        Elimination,

        /// <summary>
        /// Финальная дуэль между двумя оставшимися игроками.
        /// </summary>
        FinalDuel
    }
}
