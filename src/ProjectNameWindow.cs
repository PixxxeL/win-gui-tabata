using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScheduleTimer
{
    /// <summary>
    /// Модальное окно ввода названия проекта (стиль — как у окна итогов).
    /// Показывается при старте расписания с askProjectName, если время ещё
    /// не тратилось. Введённое имя добавляется ко всем логируемым периодам:
    /// «Работа [Проект №1]». Пустой ввод — поведение как раньше, без тега.
    /// Esc или закрытие без OK оставляют прежнее значение.
    /// </summary>
    public class ProjectNameWindow : Window
    {
        private const int MaxLength = 256;

        /// <summary>Итоговое (очищенное) название проекта; "" — без тега.</summary>
        public string ProjectName { get; private set; }

        private readonly TextBox _input;

        public ProjectNameWindow(string current)
        {
            ProjectName = current;
            ModalStyles.Setup(this, Loc.Get("project_title"));

            var panel = new StackPanel { MinWidth = 260 };
            panel.Children.Add(ModalStyles.Heading(Loc.Get("project_title")));

            panel.Children.Add(new TextBlock
            {
                Text = Loc.Get("project_prompt"),
                FontSize = 12,
                Foreground = ModalStyles.TextDim,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _input = new TextBox
            {
                Text = current,
                MaxLength = MaxLength,
                FontSize = 14,
                Padding = new Thickness(8, 6, 8, 6),
                Background = ModalStyles.Frozen(Color.FromRgb(0x26, 0x2B, 0x35)),
                Foreground = ModalStyles.TextBright,
                BorderBrush = ModalStyles.Frozen(Color.FromRgb(0x3A, 0x3F, 0x4A)),
                BorderThickness = new Thickness(1),
                CaretBrush = Brushes.White,
                SelectionBrush = ModalStyles.Frozen(Color.FromRgb(0x33, 0x66, 0x99))
            };
            panel.Children.Add(_input);

            var ok = ModalStyles.OkButton();
            ok.IsCancel = false; // Esc здесь — отмена (см. ниже), а не OK
            ok.Click += (_, _) => { ProjectName = Sanitize(_input.Text); Close(); };
            panel.Children.Add(ok);

            Content = ModalStyles.Card(panel);

            // Esc — закрыть без изменения значения
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); }
            };

            // Фокус сразу в поле ввода, курсор в конец
            Loaded += (_, _) =>
            {
                _input.Focus();
                _input.CaretIndex = _input.Text.Length;
            };
        }

        // Обрезает пробелы, выкидывает управляющие символы (переводы строк и т.п. —
        // лог построчный), страхует лимит длины (MaxLength не ловит вставку из кода).
        private static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s.Trim())
                if (!char.IsControl(ch))
                    sb.Append(ch);
            var result = sb.ToString();
            return result.Length > MaxLength ? result[..MaxLength] : result;
        }
    }
}
