using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AssemblyManager.Models;

namespace AssemblyManager.Services
{
    public sealed class AssemblyWorkspace : IDisposable
    {
        private readonly AssemblyRepository _repository;
        private readonly Control _uiContext;
        private readonly Dictionary<string, FileBinding> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _lastSync = new(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher? _watcher;

        public string? RootDirectory { get; private set; }
        public int? AssemblyId { get; private set; }
        public event Action<string>? FileSaved;

        public AssemblyWorkspace(AssemblyRepository repository, Control uiContext)
        {
            _repository = repository;
            _uiContext = uiContext;
        }

        public async Task<string?> OpenAsync(AssemblyRecord assembly)
        {
            Dispose();
            _files.Clear();
            _lastSync.Clear();

            var dir = Path.Combine(Path.GetTempPath(), "AssemblyManager", $"asm-{assembly.Id}");
            Directory.CreateDirectory(dir);
            RootDirectory = dir;
            AssemblyId = assembly.Id;

            var asmModel = await _repository.GetAssemblyModelAsync(assembly.Id);
            string? launchPath = null;
            if (asmModel is { } am)
            {
                var p = Path.Combine(dir, am.FileName);
                File.WriteAllBytes(p, am.Data);
                _files[am.FileName] = new FileBinding(FileKind.Assembly, assembly.Id);
                launchPath = p;
            }

            foreach (var part in assembly.Parts)
            {
                var partModel = await _repository.GetPartModelAsync(part.Id);
                if (partModel is not { } pm) continue;
                var p = Path.Combine(dir, pm.FileName);
                File.WriteAllBytes(p, pm.Data);
                _files[pm.FileName] = new FileBinding(FileKind.Part, part.Id);
            }

            StartWatcher(dir);
            return launchPath;
        }

        public async Task<int> SaveAllToDbAsync()
        {
            if (RootDirectory == null) return 0;
            var count = 0;
            foreach (var pair in _files)
            {
                var path = Path.Combine(RootDirectory, pair.Key);
                if (!File.Exists(path)) continue;
                var bytes = await ReadAllBytesRetryAsync(path);
                if (bytes == null) continue;
                await SaveBindingAsync(pair.Value, bytes);
                count++;
            }
            return count;
        }

        private void StartWatcher(string dir)
        {
            _watcher = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var fileName = Path.GetFileName(e.FullPath);
            if (!_files.TryGetValue(fileName, out var binding)) return;

            var now = DateTime.UtcNow;
            if (_lastSync.TryGetValue(fileName, out var last) && (now - last).TotalMilliseconds < 1500) return;
            _lastSync[fileName] = now;

            await Task.Delay(400);
            var bytes = await ReadAllBytesRetryAsync(e.FullPath);
            if (bytes == null) return;

            try
            {
                await SaveBindingAsync(binding, bytes);
                _uiContext.BeginInvoke(new Action(() => FileSaved?.Invoke(fileName)));
            }
            catch
            {
            }
        }

        private Task SaveBindingAsync(FileBinding binding, byte[] data) => binding.Kind switch
        {
            FileKind.Assembly => _repository.UpdateAssemblyModelDataAsync(binding.Id, data),
            FileKind.Part => _repository.UpdatePartModelDataAsync(binding.Id, data),
            _ => Task.CompletedTask
        };

        private static async Task<byte[]?> ReadAllBytesRetryAsync(string path)
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var buf = new byte[fs.Length];
                    await fs.ReadAsync(buf, 0, buf.Length);
                    return buf;
                }
                catch (IOException)
                {
                    await Task.Delay(200);
                }
            }
            return null;
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private enum FileKind { Assembly, Part }

        private readonly struct FileBinding
        {
            public FileKind Kind { get; }
            public int Id { get; }
            public FileBinding(FileKind kind, int id) { Kind = kind; Id = id; }
        }
    }
}
