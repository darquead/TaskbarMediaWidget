using System;

namespace TaskbarMediaWidget
{
    public static class Loc
    {
        public static string CurrentLanguage = "ru";

        public static bool IsRussian
        {
            get { return string.Equals(CurrentLanguage, "ru", StringComparison.OrdinalIgnoreCase); }
        }

        public static string AppTitle
        {
            get { return IsRussian ? "Медиаплеер на панели задач" : "Taskbar Media Widget"; }
        }

        public static string NoPlayback
        {
            get { return IsRussian ? "Нет воспроизведения" : "No playback"; }
        }

        public static string PrevTrack
        {
            get { return IsRussian ? "⏮  Предыдущий трек" : "⏮  Previous track"; }
        }

        public static string Play
        {
            get { return IsRussian ? "▶  Воспроизведение" : "▶  Play"; }
        }

        public static string Pause
        {
            get { return IsRussian ? "⏸  Пауза" : "⏸  Pause"; }
        }

        public static string NextTrack
        {
            get { return IsRussian ? "⏭  Следующий трек" : "⏭  Next track"; }
        }

        public static string FreeMode
        {
            get { return IsRussian ? "📌  Свободный режим (Desktop)" : "📌  Free Mode (Desktop)"; }
        }

        public static string CompactMode
        {
            get { return IsRussian ? "🔲  Компактный режим" : "🔲  Compact Mode"; }
        }

        public static string FontSettings
        {
            get { return IsRussian ? "🔤  Настройка шрифта..." : "🔤  Font Settings..."; }
        }

        public static string OpacityLabel(int percent)
        {
            return (IsRussian ? "👁  Прозрачность: " : "👁  Opacity: ") + percent + "%";
        }

        public static string VolumeWheel
        {
            get { return IsRussian ? "🔊  Громкость колесиком мыши" : "🔊  Mouse Wheel Volume"; }
        }

        public static string AutoStart
        {
            get { return IsRussian ? "🚀  Автозапуск с Windows" : "🚀  Start with Windows"; }
        }

        public static string ResetPosition
        {
            get { return IsRussian ? "🎯  Выровнять виджет" : "🎯  Reset / Align Position"; }
        }

        public static string LanguageMenu
        {
            get { return IsRussian ? "🌐  Язык (Language)" : "🌐  Language (Язык)"; }
        }

        public static string LangRu
        {
            get { return "Русский (Russian)"; }
        }

        public static string LangEn
        {
            get { return "English"; }
        }

        public static string GitHubLink
        {
            get { return "🐙  GitHub (@darquead)"; }
        }

        public static string Exit
        {
            get { return IsRussian ? "❌  Закрыть виджет" : "❌  Exit Widget"; }
        }

        public static string SystemVolume(int percent)
        {
            return string.Format(IsRussian ? "Система: {0}%" : "System: {0}%", percent);
        }

        public static string VolumeUp
        {
            get { return IsRussian ? "Громкость +" : "Volume +"; }
        }

        public static string VolumeDown
        {
            get { return IsRussian ? "Громкость -" : "Volume -"; }
        }
    }
}
