using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AssemblyManager.Models;

namespace AssemblyManager.Forms
{
    public class PartEditorForm : Form
    {
        private readonly bool _isEdit;
        private readonly TextBox _nameBox = new TextBox();
        private readonly TextBox _numberBox = new TextBox();
        private readonly TextBox _materialBox = new TextBox();
        private readonly TextBox _quantityBox = new TextBox();
        private readonly TextBox _modelPathBox = new TextBox();
        private readonly TextBox _notesBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

        public PartRecord Result { get; private set; }

        public PartEditorForm(PartRecord? part = null)
        {
            _isEdit = part != null;
            Result = part != null
                ? new PartRecord
                {
                    Id = part.Id,
                    AssemblyId = part.AssemblyId,
                    Name = part.Name,
                    PartNumber = part.PartNumber,
                    Material = part.Material,
                    Quantity = part.Quantity,
                    ModelPath = part.ModelPath,
                    Notes = part.Notes
                }
                : new PartRecord();

            InitializeComponent();
            _nameBox.Text = Result.Name;
            _numberBox.Text = Result.PartNumber;
            _materialBox.Text = Result.Material ?? string.Empty;
            _quantityBox.Text = Result.Quantity.ToString();
            _modelPathBox.Text = Result.ModelPath ?? string.Empty;
            _notesBox.Text = Result.Notes ?? string.Empty;
        }

        private void InitializeComponent()
        {
            Text = _isEdit ? "Изменение детали" : "Новая деталь";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 360);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(12),
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 5; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label { Text = "Название", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 0);
            _nameBox.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_nameBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Номер детали", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 1);
            _numberBox.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_numberBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Материал", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 2);
            _materialBox.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_materialBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Количество", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 3);
            _quantityBox.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_quantityBox, 1, 3);

            layout.Controls.Add(new Label { Text = "Путь к модели", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 4);
            var modelPanel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
            modelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _modelPathBox.Dock = DockStyle.Fill;
            modelPanel.Controls.Add(_modelPathBox, 0, 0);
            var browseButton = new Button { Text = "...", Width = 36, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
            browseButton.Click += OnBrowseModel;
            modelPanel.Controls.Add(browseButton, 1, 0);
            layout.Controls.Add(modelPanel, 1, 4);

            layout.Controls.Add(new Label { Text = "Примечания", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 5);
            _notesBox.Dock = DockStyle.Fill;
            _notesBox.MinimumSize = new Size(0, 80);
            layout.Controls.Add(_notesBox, 1, 5);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0)
            };
            var saveButton = new Button { Text = "Сохранить", AutoSize = true };
            saveButton.Click += OnSave;
            var cancelButton = new Button { Text = "Отмена", AutoSize = true };
            cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 6);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
        }

        private void OnBrowseModel(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Файлы моделей (*.m3d;*.cdw)|*.m3d;*.cdw|Все файлы|*.*"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var fullPath = dialog.FileName;
                if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    Result.ModelPath = fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
                }
                else
                {
                    Result.ModelPath = fullPath;
                }

                _modelPathBox.Text = Result.ModelPath;
            }
        }

        private void OnSave(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Введите название детали.", "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!int.TryParse(_quantityBox.Text, out var quantity) || quantity <= 0)
            {
                MessageBox.Show("Количество должно быть целым числом больше нуля.", "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Result.Name = _nameBox.Text.Trim();
            Result.PartNumber = _numberBox.Text.Trim();
            Result.Material = string.IsNullOrWhiteSpace(_materialBox.Text) ? null : _materialBox.Text.Trim();
            Result.Quantity = quantity;
            Result.ModelPath = string.IsNullOrWhiteSpace(_modelPathBox.Text) ? null : _modelPathBox.Text.Trim();
            Result.Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
        }
    }
}
