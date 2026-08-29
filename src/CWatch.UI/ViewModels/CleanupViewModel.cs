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
    private string _safetyFilter = "All"; // All, SafeOnly, ReviewOnly, DevOnly
    private long _totalSelectedBytes;
    private string _currentCleaningProgress = string.Empty;
    private CleanupResult? _lastResult;
    private List<CleanupCandidate> _allCandidates = [];

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

    public string SafetyFilter
    {
        get => _safetyFilter;
        set
        {
            if (SetProperty(ref _safetyFilter, value))
            {
                ApplyFilter();
            }
        }
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
        set
        {
            if (SetProperty(ref _lastResult, value))
            {
                OnPropertyChanged(nameof(HasCompletedCleanup));
            }
        }
    }

    public bool HasCompletedCleanup => LastResult != null && LastResult.BytesCleaned > 0;

    public ObservableCollection<CleanupCandidate> Candidates { get; } = [];

    public ICommand ScanCandidatesCommand { get; }
    public ICommand RequestCleanSelectedCommand { get; }
    public ICommand ConfirmCleanCommand { get; }
    public ICommand CancelConfirmationCommand { get; }
    public ICommand SelectAllSafeCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand OpenPathCommand { get; }
    public ICommand SetSafetyFilterCommand { get; }
    public ICommand DismissCelebrationCommand { get; }

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
        DismissCelebrationCommand = new RelayCommand(() => LastResult = null);

        SetSafetyFilterCommand = new RelayCommand(param =>
        {
            if (param is string filter) SafetyFilter = filter;
        });

        SelectAllSafeCommand = new RelayCommand(() =>
        {
            foreach (var c in _allCandidates) c.IsSelected = c.Safety == SafetyLevel.Safe;
            RecalculateTotal();
        });

        DeselectAllCommand = new RelayCommand(() =>
        {
            foreach (var c in _allCandidates) c.IsSelected = false;
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
        _allCandidates.Clear();
        Candidates.Clear();

        try
        {
            var results = await _cleanupEngine.ScanAllRecommendationsAsync();
            _allCandidates = results;

            ApplyFilter();
            RecalculateTotal();

            StatusMessage = _allCandidates.Count > 0
                ? $"Found {_allCandidates.Count} cleanup candidates. Total removable: {ByteSizeFormatter.Format(_allCandidates.Sum(c => c.SizeBytes))}."
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

    private void ApplyFilter()
    {
        Candidates.Clear();
        var list = _allCandidates.AsEnumerable();

        if (SafetyFilter == "SafeOnly")
        {
            list = list.Where(c => c.Safety == SafetyLevel.Safe);
        }
        else if (SafetyFilter == "ReviewOnly")
        {
            list = list.Where(c => c.Safety == SafetyLevel.Review || c.Safety == SafetyLevel.LowRisk);
        }
        else if (SafetyFilter == "DevOnly")
        {
            list = list.Where(c => c.Category == StorageCategoryType.DevelopmentTools ||
                                   c.Category == StorageCategoryType.VirtualMachinesEmulators);
        }

        foreach (var item in list)
        {
            Candidates.Add(item);
        }
    }

    public void RecalculateTotal()
    {
        TotalSelectedBytes = _allCandidates.Where(c => c.IsSelected).Sum(c => c.SizeBytes);
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

        var selected = _allCandidates.Where(c => c.IsSelected).ToList();
        var progress = new Progress<string>(msg => CurrentCleaningProgress = msg);

        try
        {
            var result = await _cleanupEngine.ExecuteCleanupAsync(selected, progress);
            LastResult = result;

            StatusMessage = $"Successfully freed {result.FormattedBytesCleaned} disk space across {result.ItemsCleanedCount} location(s)!";

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
