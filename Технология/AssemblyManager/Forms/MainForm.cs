using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AssemblyManager.Models;
using AssemblyManager.Services;
using AssemblyManager.ViewModels;

namespace AssemblyManager.Forms
{
    public class MainForm : Form
    {
        private readonly MainViewModel _viewModel;
        private readonly AssemblyRepository _repository;
        private readonly AssemblyWorkspace _workspace;
        private readonly BindingSource _assembliesSource = new BindingSource();
        private readonly BindingSource _partsSource = new BindingSource();

        private DataGridView _assembliesGrid = null!;
        private DataGridView _partsGrid = null!;
        private Label _assemblyDescription = null!;
        private Label _workspaceStatus = null!;

        public MainForm()
        {
            _repository = new AssemblyRepository();
            _viewModel = new MainViewModel(_repository, new XmlTransferService());
            _workspace = new AssemblyWorkspace(_repository, this);
            InitializeComponent();
            _workspace.FileSaved += OnWorkspaceFileSaved;
            FormClosed += (_, _) => _workspace.Dispose();
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            await _viewModel.InitializeAsync();

            _assembliesSource.DataSource = _viewModel.Assemblies;
            _partsSource.DataSource = _viewModel.Parts;

            _assembliesGrid.DataSource = _assembliesSource;
            _partsGrid.DataSource = _partsSource;

            UpdateAssemblySelection();
        }

        private void InitializeComponent()
        {
            Text = "Менеджер сборок";
            MinimumSize = new Size(1024, 640);
            StartPosition = FormStartPosition.CenterScreen;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };

            var importButton = new Button { Text = "Импорт XML", AutoSize = true, Margin = new Padding(4) };
            importButton.Click += OnImportXml;
            var exportButton = new Button { Text = "Экспорт XML", AutoSize = true, Margin = new Padding(4) };
            exportButton.Click += OnExportXml;
            var openModelButton = new Button { Text = "Открыть модель детали", AutoSize = true, Margin = new Padding(4) };
            openModelButton.Click += OnOpenModel;
            var openAssemblyButton = new Button { Text = "Открыть сборку", AutoSize = true, Margin = new Padding(4) };
            openAssemblyButton.Click += OnOpenAssembly;
            var saveWorkspaceButton = new Button { Text = "Сохранить в БД", AutoSize = true, Margin = new Padding(4) };
            saveWorkspaceButton.Click += OnSaveWorkspace;
            _workspaceStatus = new Label { AutoSize = true, Margin = new Padding(8, 10, 0, 0), ForeColor = System.Drawing.Color.DimGray };

            toolbar.Controls.Add(importButton);
            toolbar.Controls.Add(exportButton);
            toolbar.Controls.Add(openAssemblyButton);
            toolbar.Controls.Add(openModelButton);
            toolbar.Controls.Add(saveWorkspaceButton);
            toolbar.Controls.Add(_workspaceStatus);
            root.Controls.Add(toolbar, 0, 0);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            root.Controls.Add(split, 0, 1);

            // Left: assemblies
            var assembliesPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(4)
            };
            assembliesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            assembliesPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            assembliesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var assembliesLabel = new Label
            {
                Text = "Сборки",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(3, 3, 3, 6)
            };
            assembliesPanel.Controls.Add(assembliesLabel, 0, 0);

            _assembliesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                RowHeadersVisible = false
            };
            _assembliesGrid.SelectionChanged += AssembliesGrid_SelectionChanged;
            _assembliesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Код",
                DataPropertyName = nameof(AssemblyRecord.Code),
                Width = 110
            });
            _assembliesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Название",
                DataPropertyName = nameof(AssemblyRecord.Name),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            _assembliesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Обновлено",
                DataPropertyName = nameof(AssemblyRecord.UpdatedAt),
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "d" }
            });
            assembliesPanel.Controls.Add(_assembliesGrid, 0, 1);

            var assemblyButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            };
            var addAssembly = new Button { Text = "Добавить", AutoSize = true };
            addAssembly.Click += OnAddAssembly;
            var editAssembly = new Button { Text = "Изменить", AutoSize = true };
            editAssembly.Click += OnEditAssembly;
            var deleteAssembly = new Button { Text = "Удалить", AutoSize = true };
            deleteAssembly.Click += OnDeleteAssembly;
            assemblyButtons.Controls.Add(addAssembly);
            assemblyButtons.Controls.Add(editAssembly);
            assemblyButtons.Controls.Add(deleteAssembly);
            assembliesPanel.Controls.Add(assemblyButtons, 0, 2);

            split.Panel1.Controls.Add(assembliesPanel);

            // Right: parts
            var partsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(4)
            };
            partsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            partsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            partsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            partsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var partsLabel = new Label
            {
                Text = "Детали выбранной сборки",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(3, 3, 3, 2)
            };
            partsPanel.Controls.Add(partsLabel, 0, 0);

            _assemblyDescription = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = "Описание сборки будет показано здесь.",
                Padding = new Padding(0, 0, 0, 6),
                MaximumSize = new Size(int.MaxValue, 48)
            };
            partsPanel.Controls.Add(_assemblyDescription, 0, 1);

            _partsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                RowHeadersVisible = false
            };
            _partsGrid.SelectionChanged += PartsGrid_SelectionChanged;
            _partsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Номер",
                DataPropertyName = nameof(PartRecord.PartNumber),
                Width = 120
            });
            _partsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Название",
                DataPropertyName = nameof(PartRecord.Name),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            _partsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Материал",
                DataPropertyName = nameof(PartRecord.Material),
                Width = 140
            });
            _partsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Кол-во",
                DataPropertyName = nameof(PartRecord.Quantity),
                Width = 80
            });
            _partsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Файл модели",
                DataPropertyName = nameof(PartRecord.ModelFileName),
                Width = 220
            });
            partsPanel.Controls.Add(_partsGrid, 0, 2);

            var partButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0)
            };
            var addPart = new Button { Text = "Добавить деталь", AutoSize = true };
            addPart.Click += OnAddPart;
            var editPart = new Button { Text = "Изменить", AutoSize = true };
            editPart.Click += OnEditPart;
            var deletePart = new Button { Text = "Удалить", AutoSize = true };
            deletePart.Click += OnDeletePart;
            partButtons.Controls.Add(addPart);
            partButtons.Controls.Add(editPart);
            partButtons.Controls.Add(deletePart);
            partsPanel.Controls.Add(partButtons, 0, 3);

            split.Panel2.Controls.Add(partsPanel);

            Load += MainForm_Load;
        }

        private void UpdateAssemblySelection()
        {
            if (_assembliesGrid.Rows.Count > 0)
            {
                _assembliesGrid.Rows[0].Selected = true;
            }
            UpdateSelectedAssemblyFromGrid();
        }

        private void AssembliesGrid_SelectionChanged(object? sender, EventArgs e) => UpdateSelectedAssemblyFromGrid();

        private void PartsGrid_SelectionChanged(object? sender, EventArgs e)
        {
            _viewModel.SelectedPart = _partsSource.Current as PartRecord;
        }

        private void UpdateSelectedAssemblyFromGrid()
        {
            _viewModel.SelectedAssembly = _assembliesSource.Current as AssemblyRecord;
            _assemblyDescription.Text = _viewModel.SelectedAssembly?.Description ?? "Описание отсутствует.";
        }

        private async void OnAddAssembly(object? sender, EventArgs e)
        {
            using var dialog = new AssemblyEditorForm();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await _viewModel.SaveAssemblyAsync(dialog.Result);
                SelectAssembly(dialog.Result.Id);
            }
        }

        private async void OnEditAssembly(object? sender, EventArgs e)
        {
            if (_viewModel.SelectedAssembly == null)
            {
                return;
            }

            using var dialog = new AssemblyEditorForm(_viewModel.SelectedAssembly);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await _viewModel.SaveAssemblyAsync(dialog.Result);
                SelectAssembly(dialog.Result.Id);
            }
        }

        private async void OnDeleteAssembly(object? sender, EventArgs e)
        {
            if (_viewModel.SelectedAssembly == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Удалить сборку \"{_viewModel.SelectedAssembly.Name}\"?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                await _viewModel.DeleteAssemblyAsync();
                UpdateAssemblySelection();
            }
        }

        private async void OnAddPart(object? sender, EventArgs e)
        {
            if (_viewModel.SelectedAssembly == null)
            {
                MessageBox.Show("Сначала выберите сборку.", "Нет выбранной сборки", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new PartEditorForm();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                dialog.Result.AssemblyId = _viewModel.SelectedAssembly.Id;
                await _viewModel.SavePartAsync(dialog.Result);
                SelectPart(dialog.Result.Id);
            }
        }

        private async void OnEditPart(object? sender, EventArgs e)
        {
            if (_viewModel.SelectedPart == null)
            {
                return;
            }

            using var dialog = new PartEditorForm(_viewModel.SelectedPart);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await _viewModel.SavePartAsync(dialog.Result);
                SelectPart(dialog.Result.Id);
            }
        }

        private async void OnDeletePart(object? sender, EventArgs e)
        {
            if (_viewModel.SelectedPart == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Удалить деталь \"{_viewModel.SelectedPart.Name}\"?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                await _viewModel.DeletePartAsync();
                UpdateSelectedAssemblyFromGrid();
            }
        }

        private async void OnOpenModel(object? sender, EventArgs e)
        {
            var assembly = _viewModel.SelectedAssembly;
            var part = _viewModel.SelectedPart;
            if (assembly == null || part == null)
            {
                MessageBox.Show("Выберите деталь.", "Нет выбранной детали", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                await _workspace.OpenAsync(assembly);
                if (_workspace.RootDirectory == null) return;

                var partModel = await _viewModel.LoadPartModelAsync(part.Id);
                if (partModel == null)
                {
                    MessageBox.Show("У выбранной детали нет модели в БД.", "Нет файла", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var path = Path.Combine(_workspace.RootDirectory, partModel.Value.FileName);
                SetWorkspaceStatus($"Рабочая папка: {_workspace.RootDirectory}");
                KompasLauncher.Open(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnOpenAssembly(object? sender, EventArgs e)
        {
            var assembly = _viewModel.SelectedAssembly;
            if (assembly == null)
            {
                MessageBox.Show("Выберите сборку.", "Нет выбранной сборки", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var launchPath = await _workspace.OpenAsync(assembly);
                if (launchPath == null)
                {
                    MessageBox.Show("У выбранной сборки нет модели .a3d в БД.", "Нет файла", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SetWorkspaceStatus($"Рабочая папка: {_workspace.RootDirectory}");
                KompasLauncher.Open(launchPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnSaveWorkspace(object? sender, EventArgs e)
        {
            if (_workspace.RootDirectory == null)
            {
                MessageBox.Show("Сначала откройте сборку или деталь.", "Нет активной сессии", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var count = await _workspace.SaveAllToDbAsync();
                SetWorkspaceStatus($"Сохранено файлов: {count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnWorkspaceFileSaved(string fileName)
        {
            SetWorkspaceStatus($"Автосохранение: {fileName} ({DateTime.Now:HH:mm:ss})");
        }

        private void SetWorkspaceStatus(string text)
        {
            if (_workspaceStatus.InvokeRequired)
            {
                _workspaceStatus.BeginInvoke(new Action<string>(SetWorkspaceStatus), text);
                return;
            }
            _workspaceStatus.Text = text;
        }

        private void OnExportXml(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "XML файл|*.xml",
                FileName = "assemblies.xml"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _viewModel.ExportToXml(dialog.FileName);
                MessageBox.Show("Экспорт завершён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void OnImportXml(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "XML файл|*.xml"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await _viewModel.ImportFromXmlAsync(dialog.FileName);
                UpdateAssemblySelection();
                MessageBox.Show("Импорт завершён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SelectAssembly(int assemblyId)
        {
            foreach (DataGridViewRow row in _assembliesGrid.Rows)
            {
                if (row.DataBoundItem is AssemblyRecord assembly && assembly.Id == assemblyId)
                {
                    row.Selected = true;
                    _assembliesGrid.CurrentCell = row.Cells[0];
                    break;
                }
            }

            UpdateSelectedAssemblyFromGrid();
        }

        private void SelectPart(int partId)
        {
            foreach (DataGridViewRow row in _partsGrid.Rows)
            {
                if (row.DataBoundItem is PartRecord part && part.Id == partId)
                {
                    row.Selected = true;
                    _partsGrid.CurrentCell = row.Cells[0];
                    break;
                }
            }

            _viewModel.SelectedPart = _partsSource.Current as PartRecord;
        }
    }
}
