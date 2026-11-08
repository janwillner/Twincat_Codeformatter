using System;
using System.Drawing;
using System.Windows.Forms;
using TcFormatter.Formatting;

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// Options page content: the property grid and, below it, a before / after example of the selected option.
    /// The "after" text is produced with the values currently in the grid, so every change is visible at once.
    /// </summary>
    internal sealed class OptionPreviewControl : UserControl
    {
        private readonly PropertyGrid _grid;
        private readonly TextBox _before;
        private readonly TextBox _after;
        private readonly Label _title;
        private readonly Func<string, OptionPreview> _preview;

        /// <param name="preview">Example of an option (property name) with the current values, or null when there is none.</param>
        public OptionPreviewControl(Func<string, OptionPreview> preview)
        {
            _preview = preview;
            AutoScaleMode = AutoScaleMode.Font;

            _grid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = true,
                HelpVisible = true,
                PropertySort = PropertySort.Categorized,
            };
            _grid.SelectedGridItemChanged += (s, e) => UpdatePreview();
            _grid.PropertyValueChanged += (s, e) => UpdatePreview();

            var code = new Font("Consolas", 9f);
            _before = CodeBox(code);
            _after = CodeBox(code);

            _title = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold),
                Text = "Select an option to see an example.",
            };

            var examples = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            examples.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            examples.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            examples.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            examples.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            examples.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            examples.Controls.Add(_title, 0, 0);
            examples.SetColumnSpan(_title, 2);
            _title.Height = 20;
            examples.Controls.Add(new Label { Text = "Before", AutoSize = true }, 0, 1);
            examples.Controls.Add(new Label { Text = "After (with the values above)", AutoSize = true }, 1, 1);
            examples.Controls.Add(_before, 0, 2);
            examples.Controls.Add(_after, 1, 2);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2,
            };
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(examples);
            Controls.Add(split);

            // the example area keeps ~40 % of the height when the page is resized for the first time
            bool sized = false;
            split.SizeChanged += (s, e) =>
            {
                if (sized || split.Height < 200)
                    return;
                sized = true;
                split.SplitterDistance = (int)(split.Height * 0.58);
            };
        }

        /// <summary>Shows <paramref name="settings"/> in the grid (again after loading or resetting the settings).</summary>
        public void Bind(object settings)
        {
            if (!ReferenceEquals(_grid.SelectedObject, settings))
                _grid.SelectedObject = settings;
            else
                _grid.Refresh();
            UpdatePreview();
        }

        /// <summary>
        /// Diagnostics: selects every option once and reports options without a usable example.
        /// <paramref name="snapshotOption"/> stays selected at the end (for a screenshot).
        /// </summary>
        internal System.Collections.Generic.List<string> SelfTest(string snapshotOption)
        {
            var problems = new System.Collections.Generic.List<string>();
            var root = _grid.SelectedGridItem;
            while (root?.Parent != null)
                root = root.Parent;
            if (root == null)
            {
                problems.Add("the grid has no items");
                return problems;
            }

            GridItem snapshot = null;
            int count = 0;
            foreach (GridItem category in root.GridItems)
            {
                foreach (GridItem item in category.GridItems)
                {
                    count++;
                    _grid.SelectedGridItem = item;
                    if (string.IsNullOrWhiteSpace(_before.Text) || string.IsNullOrWhiteSpace(_after.Text) || _after.Text.Contains("could not be created"))
                        problems.Add($"{item.PropertyDescriptor?.Name}: '{_title.Text}' / {_after.Text}");
                    if (item.PropertyDescriptor?.Name == snapshotOption)
                        snapshot = item;
                }
            }
            if (snapshot != null)
                _grid.SelectedGridItem = snapshot;
            problems.Insert(0, $"{count} options checked");
            return problems;
        }

        private static TextBox CodeBox(Font font) => new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = font,
            BackColor = SystemColors.Window,
        };

        private void UpdatePreview()
        {
            var item = _grid.SelectedGridItem;
            string option = item?.GridItemType == GridItemType.Property ? item.PropertyDescriptor?.Name : null;
            if (option == null)
            {
                _title.Text = "Select an option to see an example.";
                _before.Text = _after.Text = string.Empty;
                return;
            }

            try
            {
                var preview = _preview(option);
                if (preview == null)
                {
                    _title.Text = item.Label + ": no example";
                    _before.Text = _after.Text = string.Empty;
                    return;
                }
                _title.Text = $"{item.Label} = {item.GetValueText()}";
                _before.Text = ToWindowsLines(preview.Before);
                _after.Text = ToWindowsLines(preview.After) + (preview.Error != null ? "\r\n\r\n// error: " + preview.Error : string.Empty);
            }
            catch (Exception ex)
            {
                _title.Text = item.Label;
                _before.Text = string.Empty;
                _after.Text = "// the example could not be created: " + ex.Message;
            }
        }

        /// <summary>The TextBox needs CRLF; the visualized text marks the real line endings itself.</summary>
        private static string ToWindowsLines(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    internal static class GridItemExtensions
    {
        /// <summary>Single line text of the value as the grid shows it.</summary>
        public static string GetValueText(this GridItem item)
        {
            string text;
            try
            {
                text = item.PropertyDescriptor?.Converter?.ConvertToString(item.Value) ?? Convert.ToString(item.Value);
            }
            catch (Exception)
            {
                text = Convert.ToString(item.Value);
            }
            text = (text ?? string.Empty).Replace("\r\n", ", ").Replace("\n", ", ");
            return text.Length > 60 ? text.Substring(0, 57) + "..." : text;
        }
    }
}
