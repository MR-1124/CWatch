using System.Collections.ObjectModel;
using System.Windows.Input;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.UI.ViewModels;

public sealed class CleanupViewModel : ViewModelBase
{
    private readonly ICleanupEngine _cleanupEngine;
    private readonly ISnapshotRepository _snapshotRepo;

    private bool _isScanning;
    private bool _isCleaning;
    private bool _showConfirmationModal;
    private string _statusMessage = "Ready to analyze safe cleanup opportunities.";
    private long _totalSelectedBytes;
    private string _currentCleaningProgress = string.Empty;
    private CleanupResult? _lastResult;

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        set => SetProperty(ref _isCleaning, value);
    }

    public bool ShowConfirmationModal
    {
        get => _showConfirmationModal;
        set => SetProperty(ref _showConfirmationModal, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public long TotalSelectedBytes
    {
        get => _totalSelectedBytes;
        set => SetProperty(ref _totalSelectedBytes, value);
    }

    public string FormattedTotalSelected
    {
        get => ByteSizeFormatter.Format(TotalSelectedBytes);
        set { }
    }

    public string CurrentCleaningProgress
    {
        get => _currentCleaningProgress;
        set => SetProperty(ref _currentCleaningProgress, value);
    }

    public CleanupResult? LastResult
    {
        get => _lastResult;
        set => SetProperty(ref _lastResult, value);
    }

    public ObservableCollection<CleanupCandidate> Candidates { get; } = [];

    public ICommand ScanCandidatesCommand { get; }
    public ICommand RequestCleanSelectedCommand { get; }
    public ICommand ConfirmCleanCommand { get; }
    public ICommand CancelConfirmationCommand { get; }
    public ICommand SelectAllSafeCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand OpenPathCommand { get; }

    public CleanupViewModel(
        ICleanupEngine cleanupEngine,
        ISnapshotRepository snapshotRepo)
    {
        _cleanupEngine = cleanupEngine;
        _snapshotRepo = snapshotRepo;

        ScanCandidatesCommand = new AsyncRelayCommand(ScanForRecommendationsAsync, () => !IsScanning && !IsCleaning);
        RequestCleanSelectedCommand = new RelayCommand(OpenConfirmationModal, () => Candidates.Any(c => c.IsSelected) && !IsCleaning);
        ConfirmCleanCommand = new AsyncRelayCommand(ExecuteSelectedCleanupAsync, () => !IsCleaning);
        CancelConfirmationCommand = new RelayCommand(() => ShowConfirmationModal = false);

        SelectAllSafeCommand = new RelayCommand(() =>
        {
            foreach (var c in Candidates) c.IsSelected = c.Safety == SafetyLevel.Safe;
            RecalculateTotal();
        });

        DeselectAllCommand = new RelayCommand(() =>
        {
            foreach (var c in Candidates) c.IsSelected = false;
            RecalculateTotal();
        });

        OpenPathCommand = new RelayCommand(param =>
        {
            if (param is CleanupCandidate c) NativeMethods.OpenInExplorer(c.Path);
        });
    }

    public async Task ScanForRecommendationsAsync()
    {
        IsScanning = true;
        StatusMessage = "Analyzing system temp, caches, and developer environments...";
        Candidates.Clear();
        LastResult = null;

        try
        {
            var results = await _cleanupEngine.ScanAllRecommendationsAsync();
            foreach (var item in results)
            {
                Candidates.Add(item);
            }

            RecalculateTotal();
            StatusMessage = Candidates.Count > 0
                ? $"Found {Candidates.Count} cleanup candidates. Total removable: {ByteSizeFormatter.Format(Candidates.Sum(c => c.SizeBytes))}."
                : "No disposable junk found. Drive is clean!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void RecalculateTotal()
    {
        TotalSelectedBytes = Candidates.Where(c => c.IsSelected).Sum(c => c.SizeBytes);
        OnPropertyChanged(nameof(FormattedTotalSelected));
    }

    private void OpenConfirmationModal()
    {
        RecalculateTotal();
        if (TotalSelectedBytes > 0)
        {
            ShowConfirmationModal = true;
        }
    }

    private async Task ExecuteSelectedCleanupAsync()
    {
        ShowConfirmationModal = false;
        IsCleaning = true;
        StatusMessage = "Performing safe cleanup...";

        var selected = Candidates.Where(c => c.IsSelected).ToList();
        var progress = new Progress<string>(msg => CurrentCleaningProgress = msg);

        try
        {
            var result = await _cleanupEngine.ExecuteCleanupAsync(selected, progress);
            LastResult = result;

            StatusMessage = $"Successfully freed {result.FormattedBytesCleaned} disk space!";

            // Refresh candidate list after cleaning
            await ScanForRecommendationsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cleanup failed: {ex.Message}";
        }
        finally
        {
            IsCleaning = false;
            CurrentCleaningProgress = string.Empty;
        }
    }
}
