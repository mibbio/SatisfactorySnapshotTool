namespace SatisfactorySnapshotTool.Backup
{
    using Newtonsoft.Json;

    using SatisfactorySnapshotTool.Events;
    using SatisfactorySnapshotTool.Models;
    using SatisfactorySnapshotTool.Mvvm;

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    public sealed class BackupManager : NotifyPropertyChangedBase
    {
        #region fields
        private const int _bufferSize = 1024 * 64;

        private const string _backupGameSubdir = "game";

        private const string _backupSaveSubdir = "saves";

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private Dictionary<string, Tuple<Guid, string>> _checksumCache;

        private readonly ISettings _settings;

        private bool _gameRunning = false;
        #endregion

        #region properties
        public ObservableCollection<BackupModel> Backups { get; } = new ObservableCollection<BackupModel>();

        public bool GameRunning
        {
            get => _gameRunning;
            private set
            {
                if (_gameRunning == value) return;
                _gameRunning = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region constructors
        public BackupManager(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException();
            _checksumCache = new Dictionary<string, Tuple<Guid, string>>();
            if (Directory.Exists(settings.BackupPath))
            {
                foreach (var path in Directory.EnumerateDirectories(settings.BackupPath))
                {
                    try
                    {
                        var model = new BackupModel(Guid.Parse(Path.GetFileNameWithoutExtension(path)));
                        var json = File.ReadAllText(Path.Combine(path, "backup.json"));
                        JsonConvert.PopulateObject(json, model);
                        PopulateSavegames(model);
                        Backups.Add(model);
                    }
                    catch (Exception ex) when (ex is FileNotFoundException || ex is FormatException || ex is IOException)
                    {
                        return;
                    }
                }
                UpdateChecksumCache();
            }

            Backups.CollectionChanged += (s, e) => UpdateChecksumCache();
        }
        #endregion

        #region methods
        public async Task CreateBackup() => await CreateBackup(_settings.GamePath).ConfigureAwait(false);

        public async Task CreateBackup(string sourceRootPath)
        {
            // exp: "++FactoryGame+main-CL-109370"
            // stable: "++FactoryGame+rel-main-ea-bu1-slim-CL-109075"
            var backupGuid = Guid.NewGuid();
            var targetDirectory = Path.Combine(_settings.BackupPath, backupGuid.ToString());

            _cts = new CancellationTokenSource();

            try
            {
                var rawSources = IndexFiles(sourceRootPath);
                var gameFileGroup = await GenerateFileGroup(rawSources, sourceRootPath, true, _cts.Token).ConfigureAwait(false);

                var rawSaves = IndexFiles(_settings.SavegamePath);
                var savesFileGroup = await GenerateFileGroup(rawSaves, _settings.SavegamePath, false, _cts.Token).ConfigureAwait(false);

                OnBackupStep?.Invoke(this, new BackupEventArgs(BackupStep.CopyGameFile, gameFileGroup.FilesToCopy.Count));
                var gameSize = await CopyFilesAsync(gameFileGroup, sourceRootPath, Path.Combine(targetDirectory, _backupGameSubdir), _cts.Token);

                // create backup metadata
                GameBranch branch = gameFileGroup.Version.Contains("-ea-") ? GameBranch.Stable : GameBranch.Experimental;
                var model = new BackupModel(backupGuid, branch, gameFileGroup.Build, DateTime.UtcNow);

                // hardlink files
                foreach (var dependency in gameFileGroup.Dependencies)
                {
                    var sourceFile = Path.Combine(targetDirectory, _backupGameSubdir, dependency.Key);
                    var targetFile = Path.Combine(_settings.BackupPath, dependency.Value.ToString(), _backupGameSubdir, dependency.Key);
                    FileHelper.CreateHardLink(sourceFile, targetFile);
                    model.AddDependency(dependency.Value, dependency.Key);
                }

                OnBackupStep?.Invoke(this, new BackupEventArgs(BackupStep.CopySaveFile, savesFileGroup.FilesToCopy.Count));
                var savesSize = await CopyFilesAsync(savesFileGroup, _settings.SavegamePath, Path.Combine(targetDirectory, _backupSaveSubdir), _cts.Token);

                model.AddChecksums(gameFileGroup.Checksums);
                model.BackupSize = gameSize + savesSize;
                PopulateSavegames(model);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Backups.Add(model);
                });

                var json = JsonConvert.SerializeObject(model, Formatting.Indented);
                File.WriteAllText(Path.Combine(targetDirectory, "backup.json"), json);
            }
            catch (Exception ex) when (ex is IOException || ex is TaskCanceledException)
            {
                // rollback (delete already copied files)
                if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
                MessageBox.Show(ex.Message, "Backup not successfull", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                OnBackupStep?.Invoke(this, new BackupEventArgs(BackupStep.None, 0));
            }
        }

        private static IEnumerable<string> IndexFiles(string rootPath, int maxDepth = 16)
        {
            if (!File.GetAttributes(rootPath).HasFlag(FileAttributes.Directory))
            {
                throw new ArgumentException("{0} is a file and not a directory.", nameof(rootPath));
            }

            if (Directory.EnumerateFileSystemEntries(rootPath).Any())
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(rootPath))
                {
                    if (File.GetAttributes(entry).HasFlag(FileAttributes.Directory))
                    {
                        if (maxDepth == 0) break;
                        foreach (var subEntryUri in IndexFiles(entry, maxDepth - 1))
                        {
                            yield return subEntryUri;
                        }
                    }
                    else
                    {
                        yield return entry;
                    }
                }
            }
            else yield return rootPath;
        }

        private async Task<BackupFileGroup> GenerateFileGroup(IEnumerable<string> pathList, string referencePath, bool dedup, CancellationToken ct)
        {
            int refPathLength = (!string.IsNullOrEmpty(referencePath)) ? referencePath.Length : 0;
            BackupFileGroup result = new BackupFileGroup();

            var directories = pathList.Where(path => File.GetAttributes(path).HasFlag(FileAttributes.Directory));
            var files = pathList.Except(directories);

            foreach (var directory in directories)
            {
                result.AddDirectory(directory.Remove(0, refPathLength).TrimStart(Path.DirectorySeparatorChar));
            }

            OnBackupStep?.Invoke(this, new BackupEventArgs(BackupStep.CalculateChecksums, files.Count()));

            foreach (var path in files)
            {
                // populate version info
                if (FileHelper.GameExecutableNames.Contains(Path.GetFileName(path)))
                {
                    FileHelper.TryGetBuild(path, out result.Version, out result.Build);
                }

                var relativePath = path.Remove(0, refPathLength).TrimStart(Path.DirectorySeparatorChar);

                // calculate checksum
                if (dedup)
                {
                    var checksum = await Task.Run(() =>
                    {
                        //var fileCount = result.Checksums.Count + result.Dependencies.Count + 1;
                        //OnFileProcessing?.Invoke(this, new FileProcessEventArgs(path, fileCount));
                        OnFileProcessing?.Invoke(this, new FileProcessEventArgs(path, result.Checksums.Count + 1));
                        ct.ThrowIfCancellationRequested();
                        return FileHelper.GetMD5WithProgress(path, OnFileProgress, ct);
                    }, ct);

                    if (_checksumCache.TryGetValue(checksum, out var dependency) && path.Contains(dependency.Item2))
                    {
                        result.AddDependency(dependency.Item1, dependency.Item2, checksum);
                    }
                    else
                    {
                        result.AddFile(relativePath, checksum);
                    }
                }
                else
                {
                    result.AddFile(relativePath);
                }
            }
            return result;
        }

        private async Task<long> CopyFileAsync(string sourcePath, string destPath, CancellationToken ct)
        {
            if (!File.Exists(sourcePath))
            {
                throw new ArgumentException($"File {sourcePath} does not exist.");
            }
            if (File.GetAttributes(sourcePath).HasFlag(FileAttributes.Directory))
            {
                throw new ArgumentException($"{sourcePath} is not a file.");
            }

            try
            {
                using (FileStream input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, FileOptions.SequentialScan))
                using (FileStream output = new FileStream(destPath, FileMode.Create))
                {
                    int notifyStep = (int)(input.Length / 200); // notify progress every 0.5% only
                    int copiedSinceLastEvent = 0;
                    long totalCopiedBytes = 0;

                    byte[] buffer = new byte[_bufferSize];
                    int dataLength;
                    do
                    {
                        dataLength = await input.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                        await output.WriteAsync(buffer, 0, dataLength, ct).ConfigureAwait(false);

                        totalCopiedBytes += dataLength;
                        copiedSinceLastEvent += dataLength;
                        if (copiedSinceLastEvent >= notifyStep)
                        {
                            copiedSinceLastEvent = 0;
                            OnFileProgress?.Invoke(this, new FileProgressEventArgs(sourcePath, totalCopiedBytes, input.Length));
                        }
                    } while (input.Position < input.Length);
                    return totalCopiedBytes;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task<long> CopyFilesAsync(BackupFileGroup data, string srcRootPath, string destRootPath, CancellationToken ct)
        {
            long copiedSize = 0;
            try
            {
                // create directory structure
                string dirPath;
                foreach (var dir in data.NeededDirectories)
                {
                    dirPath = Path.Combine(destRootPath, dir);
                    Directory.CreateDirectory(dirPath);
                }

                // copy files
                int counter = 0;
                foreach (var file in data.FilesToCopy)
                {
                    OnFileProcessing?.Invoke(this, new FileProcessEventArgs(file, ++counter));
                    var srcFile = Path.Combine(srcRootPath, file);
                    var destFile = Path.Combine(destRootPath, file);
                    copiedSize += await CopyFileAsync(srcFile, destFile, ct);
                }
                return copiedSize;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException("Error while copying files.", ex);
            }
        }

        public bool DeleteBackup(BackupModel backup)
        {
            var index = Backups.IndexOf(backup);
            if (index >= 0)
            {
                var backupDir = Path.Combine(_settings.BackupPath, Backups[index].Guid.ToString());
                if (Directory.Exists(backupDir))
                {
                    try
                    {
                        Directory.Delete(backupDir, true);
                        Backups.Remove(backup);
                        RecalculateBackupSizes(backup.Guid);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        public async Task Launch(BackupModel backup)
        {
            GameRunning = true;

            var tempDir = Directory.CreateDirectory(Path.Combine(_settings.BackupPath, "temp"));
            if (tempDir.Exists) { tempDir.Delete(true); }

            // temporary backup of existing saves
            foreach (var dir in Directory.EnumerateDirectories(_settings.SavegamePath))
            {
                var targetDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, Path.GetFileName(dir)));
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    File.Copy(file, Path.Combine(targetDir.FullName, Path.GetFileName(file)));
                }
                Directory.Delete(dir, true);
            }

            // copy backup saves to working dir
            var backupSaveRoot = Path.Combine(_settings.BackupPath, backup.Guid.ToString(), _backupSaveSubdir);
            foreach (var dir in Directory.EnumerateDirectories(backupSaveRoot))
            {
                var targetDir = Directory.CreateDirectory(Path.Combine(_settings.SavegamePath, Path.GetFileName(dir)));
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    File.Copy(file, Path.Combine(targetDir.FullName, Path.GetFileName(file)));
                }
            }

            var workingDir = Path.Combine(_settings.BackupPath, backup.Guid.ToString(), _backupGameSubdir);
            var executable = Path.Combine(workingDir, "FactoryGame.exe");
            var psi = new ProcessStartInfo(executable, "-EpicPortal");
            psi.WorkingDirectory = workingDir;

            var process = Process.Start(psi);
            while (!process.HasExited) await Task.Delay(100);
            process.Close();

            // update saves in backup
            foreach (var dir in Directory.EnumerateDirectories(_settings.SavegamePath))
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var targetFile = Path.Combine(backupSaveRoot, Path.GetFileName(file));
                    try
                    {
                        File.Copy(file, targetFile, true);
                    }
                    catch (IOException)
                    {
                        throw;
                    }
                }
                Directory.Delete(dir, true);
            }

            // copy temporary backup back to working dir
            foreach (var dir in tempDir.EnumerateDirectories())
            {
                var targetDir = Directory.CreateDirectory(Path.Combine(_settings.SavegamePath, Path.GetFileName(dir.FullName)));
                foreach (var file in dir.EnumerateFiles())
                {
                    var targetFile = Path.Combine(targetDir.FullName, Path.GetFileName(file.FullName));
                    File.Copy(file.FullName, targetFile);
                }
            }
            if (tempDir.Exists) tempDir.Delete(true);

            GameRunning = false;
        }

        public void CancelBackup()
        {
            _cts.Cancel();
        }

        private void RecalculateBackupSizes(Guid deletedBackupGuid)
        {
            var dependents = Backups.Where(bm => bm.Dependencies.ContainsKey(deletedBackupGuid));

            foreach (var bm in dependents)
            {
                var backupDir = Path.Combine(_settings.BackupPath, bm.Guid.ToString());
                var additionalSize = bm.Dependencies[deletedBackupGuid].Sum(file =>
                {
                    var fi = new FileInfo(Path.Combine(backupDir, _backupGameSubdir, file));
                    return fi.Length;
                });
                bm.BackupSize += additionalSize;
                bm.Dependencies.Remove(deletedBackupGuid);

                var json = JsonConvert.SerializeObject(bm, Formatting.Indented);
                File.WriteAllText(Path.Combine(backupDir, "backup.json"), json);
            }
        }

        private void PopulateSavegames(BackupModel backupModel)
        {
            var savesRoot = Path.Combine(_settings.BackupPath, backupModel.Guid.ToString(), _backupSaveSubdir);
            foreach (var subdir in Directory.EnumerateDirectories(savesRoot))
            {
                foreach (var save in Directory.EnumerateFiles(subdir))
                {
                    backupModel.AddSave(new SavegameHeader(save));
                } 
            }
        }

        private void UpdateChecksumCache()
        {
            _checksumCache = Backups?.Select(bm => bm.Checksums.Select(kvp => new KeyValuePair<string, Tuple<Guid, string>>(kvp.Key, Tuple.Create(bm.Guid, kvp.Value))))
                .SelectMany(dict => dict)
                .GroupBy(pair => pair.Key)
                .ToDictionary(group => group.Key, group => group.First().Value);
        }
        #endregion

        #region events
        public event EventHandler<BackupEventArgs> OnBackupStep;

        public event EventHandler<FileProcessEventArgs> OnFileProcessing;

        public event EventHandler<FileProgressEventArgs> OnFileProgress;
        #endregion
    }
}
