using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AssemblyManager.Models;

namespace AssemblyManager.Forms
{
    public class AssemblyEditorForm : Form
    {
        private readonly bool _isEdit;
        private readonly TextBox _nameBox = new TextBox();
        private readonly TextBox _codeBox = new TextBox();
        private readonly TextBox _descriptionBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _modelBox = new TextBox { ReadOnly = true };
        private byte[]? _modelData;
        private string? _modelFileName;

        public AssemblyRecord Result { get; private set; }

        public AssemblyEditorForm(AssemblyRecord? assembly = null)
        {
            _isEdit = assembly != null;
            Result = assembly != null
                ? new AssemblyRecord
                {
                    Id = assembly.Id,
                    Name = assembly.Name,
                    Code = assembly.Code,
                    Description = assembly.Description,
                    ModelFileName = assembly.ModelFileName,
                    ModelData = assembly.ModelData,
                    CreatedAt = assembly.CreatedAt,
                    UpdatedAt = assembly.UpdatedAt
                }
                : new AssemblyRecord
                {
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

            InitializeComponent();
            _nameBox.Text = Result.Name;
            _codeBox.Text = Result.Code;
            _descriptionBox.Text = Result.Description ?? string.Empty;
            _modelFileName = Result.ModelFileName;
            _modelData = Result.ModelData;
            _modelBox.Text = _modelFileName ?? string.Empty;
        }

        private void InitializeComponent()
        {
            Text = _isEdit ? "Изменение сборки" : "Новая сборка";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(480, 320);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(12),
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label { Text = "Название", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 0);
            _nameBox.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_nameBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Код", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 1);
            _codeBox.Margin = new Padding(0, 0, 0, 8);
            layout.Controls.Add(_codeBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Описание", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 2);
            _descriptionBox.Dock = DockStyle.Fill;
            _descriptionBox.MinimumSize = new Size(0, 80);
            layout.Controls.Add(_descriptionBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Файл сборки", AutoSize = true, Margin = new Padding(0, 4, 8, 8) }, 0, 3);
            var modelPanel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
            modelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _modelBox.Dock = DockStyle.Fill;
            modelPanel.Controls.Add(_modelBox, 0, 0);
            var browse = new Button { Text = "...", Width = 36, AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
            browse.Click += OnBrowseModel;
            modelPanel.Controls.Add(browse, 1, 0);
            layout.Controls.Add(modelPanel, 1, 3);

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
            layout.Controls.Add(buttons, 0, 4);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
        }

        private void OnSave(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Введите название сборки.", "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Result.Name = _nameBox.Text.Trim();
            Result.Code = _codeBox.Text.Trim();
            Result.Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
            Result.ModelFileName = _modelFileName;
            Result.ModelData = _modelData;
            Result.UpdatedAt = DateTime.Now;
            DialogResult = DialogResult.OK;
        }

        private void OnBrowseModel(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Файлы сборок (*.a3d)|*.a3d|Все файлы|*.*"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _modelFileName = Path.GetFileName(dialog.FileName);
                _modelData = File.ReadAllBytes(dialog.FileName);
                _modelBox.Text = _modelFileName;
            }
        }
    }
}
