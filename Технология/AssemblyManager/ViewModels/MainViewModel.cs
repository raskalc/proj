using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AssemblyManager.Models;
using AssemblyManager.Services;

namespace AssemblyManager.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly AssemblyRepository _repository;
        private readonly XmlTransferService _xmlTransferService;

        public BindingList<AssemblyRecord> Assemblies { get; } = new BindingList<AssemblyRecord>();
        public BindingList<PartRecord> Parts { get; } = new BindingList<PartRecord>();

        private AssemblyRecord? _selectedAssembly;
        public AssemblyRecord? SelectedAssembly
        {
            get => _selectedAssembly;
            set
            {
                if (SetProperty(ref _selectedAssembly, value))
                {
                    UpdateParts();
                }
            }
        }

        private PartRecord? _selectedPart;
        public PartRecord? SelectedPart
        {
            get => _selectedPart;
            set => SetProperty(ref _selectedPart, value);
        }

        public MainViewModel(AssemblyRepository repository, XmlTransferService xmlTransferService)
        {
            _repository = repository;
            _xmlTransferService = xmlTransferService;
        }

        public async Task InitializeAsync()
        {
            await _repository.EnsureCreatedAsync();
            var existing = await _repository.GetAssembliesAsync();

            // Если сборок больше одной – оставляем только первую.
            if (existing.Count > 1)
            {
                var first = existing.First();
                await _repository.ReplaceAllAsync(new List<AssemblyRecord> { first });
            }

            await LoadAssembliesAsync();
        }

        public async Task LoadAssembliesAsync()
        {
            Assemblies.Clear();
            var assemblies = await _repository.GetAssembliesAsync();
            foreach (var assembly in assemblies)
            {
                Assemblies.Add(assembly);
            }

            SelectedAssembly = Assemblies.FirstOrDefault();
        }

        private void UpdateParts()
        {
            Parts.Clear();
            if (SelectedAssembly?.Parts == null)
            {
                return;
            }

            foreach (var part in SelectedAssembly.Parts.OrderBy(p => p.PartNumber))
            {
                Parts.Add(part);
            }

            SelectedPart = Parts.FirstOrDefault();
        }

        public async Task<AssemblyRecord?> SaveAssemblyAsync(AssemblyRecord assembly)
        {
            var saved = await _repository.SaveAssemblyAsync(assembly);
            await LoadAssembliesAsync();
            SelectedAssembly = Assemblies.FirstOrDefault(a => a.Id == saved.Id) ?? Assemblies.FirstOrDefault();
            return saved;
        }

        public async Task DeleteAssemblyAsync()
        {
            if (SelectedAssembly == null)
            {
                return;
            }

            await _repository.DeleteAssemblyAsync(SelectedAssembly.Id);
            await LoadAssembliesAsync();
        }

        public async Task SavePartAsync(PartRecord part)
        {
            if (SelectedAssembly == null)
            {
                return;
            }

            var currentAssemblyId = SelectedAssembly.Id;
            await _repository.SavePartAsync(currentAssemblyId, part);
            await LoadAssembliesAsync();
            SelectedAssembly = Assemblies.FirstOrDefault(a => a.Id == currentAssemblyId);
        }

        public async Task DeletePartAsync()
        {
            if (SelectedPart == null || SelectedAssembly == null)
            {
                return;
            }

            var currentAssemblyId = SelectedAssembly.Id;
            await _repository.DeletePartAsync(SelectedPart.Id);
            await LoadAssembliesAsync();
            SelectedAssembly = Assemblies.FirstOrDefault(a => a.Id == currentAssemblyId);
        }

        public void ExportToXml(string filePath)
        {
            _xmlTransferService.Export(filePath, Assemblies);
        }

        public async Task ImportFromXmlAsync(string filePath)
        {
            var assemblies = _xmlTransferService.Import(filePath);
            await _repository.ReplaceAllAsync(assemblies);
            await LoadAssembliesAsync();
        }
    }
}
