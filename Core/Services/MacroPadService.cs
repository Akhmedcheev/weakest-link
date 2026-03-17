using System.Threading.Tasks;

namespace WeakestLink.Core.Services
{
    /// <summary>
    /// Макропад программируется через MINI KeyBoard.exe (оригинальное ПО).
    /// USB-программирование невозможно: устройство не поддерживает Output/Feature Reports.
    /// Кнопки должны быть настроены на A, B, C, D, E, F.
    /// Клавиши перехватываются в OperatorPanel через PreviewKeyDown:
    ///   A = ВЕРНО, B = НЕВЕРНО, C = БАНК, D = READY, F = START O'CLOCK
    /// </summary>
    public class MacroPadService
    {
        public static Task<(bool Success, string Message)> ProgramMacroPadAsync()
        {
            return Task.FromResult((false,
                "Макропад программируется через оригинальное ПО (MINI KeyBoard.exe).\n" +
                "Назначьте кнопки: A, B, C, D, E, F.\n" +
                "Горячие клавиши: A=ВЕРНО, B=НЕВЕРНО, C=БАНК, D=READY, F=START"));
        }
    }
}
