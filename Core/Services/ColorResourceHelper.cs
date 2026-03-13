using System.Windows;
using System.Windows.Media;

namespace WeakestLink.Core.Services;

/// <summary>
/// Помощник для доступа к XAML ресурсам Color Bible из code-behind.
/// Позволяет использовать одну палитру цветов везде (XAML + C#).
/// </summary>
public static class ColorResourceHelper
{
    /// <summary>Базовый фон (Deep Charcoal #1E1E1E)</summary>
    public static SolidColorBrush ObsidianBg => GetBrush(nameof(ObsidianBg));

    /// <summary>Фон поверхности (Panel Background #252525)</summary>
    public static SolidColorBrush ObsidianSurface => GetBrush(nameof(ObsidianSurface));

    /// <summary>Приподнятая карточка (#333333)</summary>
    public static SolidColorBrush ObsidianRaised => GetBrush(nameof(ObsidianRaised));

    /// <summary>Основной текст (#E0E0E0)</summary>
    public static SolidColorBrush ObsidianTextPrimary => GetBrush(nameof(ObsidianTextPrimary));

    /// <summary>Вторичный текст (#999999)</summary>
    public static SolidColorBrush ObsidianTextSecondary => GetBrush(nameof(ObsidianTextSecondary));

    /// <summary>Приглушённый текст для labels (#555555)</summary>
    public static SolidColorBrush ObsidianTextMuted => GetBrush(nameof(ObsidianTextMuted));

    /// <summary>Отключённый текст (#717171)</summary>
    public static SolidColorBrush ObsidianTextDisabled => GetBrush(nameof(ObsidianTextDisabled));

    // ═══ SEMANTIC COLORS ═══

    /// <summary>Успех - Верно (Зелёный #22C55E)</summary>
    public static SolidColorBrush ColorSuccess => GetBrush(nameof(ColorSuccess));

    /// <summary>Опасность - Неверно (Красный #EF4444)</summary>
    public static SolidColorBrush ColorDanger => GetBrush(nameof(ColorDanger));

    /// <summary>Предупреждение - Банк (Жёлтый #F59E0B)</summary>
    public static SolidColorBrush ColorWarning => GetBrush(nameof(ColorWarning));

    /// <summary>Информация (Синий #4F6BED)</summary>
    public static SolidColorBrush ColorInfo => GetBrush(nameof(ColorInfo));

    // ═══ GAME COLORS ═══

    /// <summary>Верно (зелёный #23a559)</summary>
    public static SolidColorBrush ColorGreen => GetBrush(nameof(ColorGreen));

    /// <summary>Банк/Деньги (золотистый #FFD700)</summary>
    public static SolidColorBrush ColorGold => GetBrush(nameof(ColorGold));

    /// <summary>Неверно (красный #da373c)</summary>
    public static SolidColorBrush ColorRed => GetBrush(nameof(ColorRed));

    /// <summary>Первичное действие (Blurple #5865F2)</summary>
    public static SolidColorBrush ColorBlurple => GetBrush(nameof(ColorBlurple));

    /// <summary>Предупреждение (оранжевый #FF8C00)</summary>
    public static SolidColorBrush ColorOrange => GetBrush(nameof(ColorOrange));

    /// <summary>Accent Teal (#2DD4BF)</summary>
    public static SolidColorBrush ColorTeal => GetBrush(nameof(ColorTeal));

    // ═══ STATUS COLORS ═══

    /// <summary>Статус Online (зелёный)</summary>
    public static SolidColorBrush StatusOnline => GetBrush(nameof(StatusOnline));

    /// <summary>Статус Warning (жёлтый)</summary>
    public static SolidColorBrush StatusWarning => GetBrush(nameof(StatusWarning));

    /// <summary>Статус Offline (серый)</summary>
    public static SolidColorBrush StatusOffline => GetBrush(nameof(StatusOffline));

    // ═══ INTERNAL HELPER ═══

    /// <summary>
    /// Получает brush ресурс из App.xaml ресурсов.
    /// Если ресурс не найден, возвращает белый цвет (безопасно).
    /// </summary>
    private static SolidColorBrush GetBrush(string resourceName)
    {
        try
        {
            if (Application.Current?.Resources[resourceName] is SolidColorBrush brush)
            {
                return brush;
            }
        }
        catch
        {
            // Fallback если ресурс не найден
        }

        // Безопасный fallback
        return new SolidColorBrush(Colors.White);
    }
}
