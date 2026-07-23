using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScheduleTimer
{
    /// <summary>
    /// Общий стиль модальных окон приложения (итоги, ввод названия проекта):
    /// тёмная скруглённая «карточка» без системной рамки + кнопка OK.
    /// </summary>
    internal static class ModalStyles
    {
        public static readonly Brush TextDim = Frozen(Color.FromRgb(0xB9, 0xBE, 0xC7));
        public static readonly Brush TextBright = Frozen(Color.FromRgb(0xE8, 0xEA, 0xED));

        public static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>Общие свойства окна: без рамки, прозрачное, по центру владельца.</summary>
        public static void Setup(Window w, string title)
        {
            w.WindowStyle = WindowStyle.None;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.ResizeMode = ResizeMode.NoResize;
            w.SizeToContent = SizeToContent.WidthAndHeight;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            w.ShowInTaskbar = false;
            w.FontFamily = new FontFamily("Segoe UI");
            w.Title = title;
            w.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) w.DragMove();
            };
        }

        /// <summary>Корневая «карточка» в стиле главного окна.</summary>
        public static Border Card(UIElement child) => new()
        {
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            BorderBrush = Frozen(Color.FromRgb(0x2A, 0x2E, 0x37)),
            Background = new LinearGradientBrush(
                Color.FromRgb(0x14, 0x17, 0x1D), Color.FromRgb(0x1B, 0x1F, 0x27), 90),
            Padding = new Thickness(28, 22, 28, 22),
            Child = child
        };

        /// <summary>Заголовок карточки.</summary>
        public static TextBlock Heading(string text) => new()
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBright,
            Margin = new Thickness(0, 0, 0, 14)
        };

        /// <summary>Кнопка OK (Enter). Esc обрабатывает IsCancel.</summary>
        public static Button OkButton()
        {
            return new Button
            {
                Content = "OK",
                IsDefault = true,   // Enter
                IsCancel = true,    // Esc
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = TextBright,
                FontSize = 13,
                Template = OkTemplate()
            };
        }

        // Скруглённая кнопка с лёгкой подсветкой при наведении
        private static ControlTemplate OkTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border), "bd");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, Frozen(Color.FromRgb(0x26, 0x2B, 0x35)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.BorderBrushProperty, Frozen(Color.FromRgb(0x3A, 0x3F, 0x4A)));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(28, 7, 28, 7));
            border.AppendChild(presenter);

            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty,
                Frozen(Color.FromRgb(0x32, 0x38, 0x45)), "bd"));
            template.Triggers.Add(hover);
            return template;
        }
    }

    /// <summary>
    /// Модальное окно итогов: сколько времени отработано по каждому периоду
    /// (подготовка не учитывается, паузы исключены) и общий итог.
    /// Показывается по «Стоп» и при полном завершении сценария.
    /// UI собирается кодом — окно маленькое, отдельный XAML не оправдан.
    /// </summary>
    public class SummaryWindow : Window
    {
        public SummaryWindow(IReadOnlyList<(string Name, int Color, int Seconds)> items)
        {
            ModalStyles.Setup(this, Loc.Get("summary_title"));

            var panel = new StackPanel { MinWidth = 240 };
            panel.Children.Add(ModalStyles.Heading(Loc.Get("summary_title")));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // цветная точка
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // имя
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // время

            int row = 0, total = 0;
            foreach (var (name, color, seconds) in items)
            {
                total += seconds;
                AddRow(grid, row++, name, color, seconds);
            }

            // Разделитель и «Итого» — когда периодов больше одного
            if (items.Count > 1)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                var sep = new Border
                {
                    Height = 1,
                    Background = ModalStyles.Frozen(Color.FromRgb(0x2A, 0x2E, 0x37)),
                    Margin = new Thickness(0, 6, 0, 8)
                };
                Grid.SetRow(sep, row); Grid.SetColumnSpan(sep, 3);
                grid.Children.Add(sep);
                row++;

                AddRow(grid, row, Loc.Get("summary_total"), color: null, total, bold: true);
            }

            panel.Children.Add(grid);

            var ok = ModalStyles.OkButton();
            ok.Click += (_, _) => Close();
            panel.Children.Add(ok);

            Content = ModalStyles.Card(panel);
        }

        private static void AddRow(Grid grid, int row, string name, int? color, int seconds, bool bold = false)
        {
            grid.RowDefinitions.Add(new RowDefinition());

            if (color is { } c)
            {
                var dot = new Ellipse
                {
                    Width = 9, Height = 9,
                    Fill = ModalStyles.Frozen(Color.FromRgb((byte)(c >> 16), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF))),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 9, 0)
                };
                Grid.SetRow(dot, row); Grid.SetColumn(dot, 0);
                grid.Children.Add(dot);
            }

            var lblName = new TextBlock
            {
                Text = name,
                Foreground = bold ? ModalStyles.TextBright : ModalStyles.TextDim,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 13,
                Margin = new Thickness(0, 3, 24, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lblName, row); Grid.SetColumn(lblName, 1);
            grid.Children.Add(lblName);

            var lblTime = new TextBlock
            {
                Text = Format(seconds),
                Foreground = ModalStyles.TextBright,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 3)
            };
            Grid.SetRow(lblTime, row); Grid.SetColumn(lblTime, 2);
            grid.Children.Add(lblTime);
        }

        /// <summary>
        /// «45 сек» / «3 мин 20 сек» / «1 ч 2 мин 5 сек» / «1 д 3 ч 0 мин 10 сек».
        /// Старшие разряды появляются, когда значение до них дотягивает.
        /// </summary>
        public static string Format(int seconds)
        {
            int d = seconds / 86400, h = seconds % 86400 / 3600, m = seconds % 3600 / 60, s = seconds % 60;
            var parts = new List<string>(4);
            if (d > 0) parts.Add($"{d} {Loc.Get("unit_d")}");
            if (parts.Count > 0 || h > 0) parts.Add($"{h} {Loc.Get("unit_h")}");
            if (parts.Count > 0 || m > 0) parts.Add($"{m} {Loc.Get("unit_m")}");
            parts.Add($"{s} {Loc.Get("unit_s")}");
            return string.Join(" ", parts);
        }
    }
}
