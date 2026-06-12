using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Infrastructure.Data;
using TradeScanner.UI.Infrastructure;

namespace TradeScanner.UI.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly TradeScannerDbContext _db;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private string _saveStatus = string.Empty;

    public IReadOnlyList<ScanFrequency> ScanFrequencies { get; } = Enum.GetValues<ScanFrequency>();
    public IReadOnlyList<string> Themes { get; } = ["Dark", "Light"];

    public SettingsViewModel(TradeScannerDbContext db)
    {
        _db = db;
    }

    public async Task LoadAsync()
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync();
        if (settings != null) Settings = settings;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var existing = await _db.AppSettings.FirstOrDefaultAsync();
            if (existing == null)
                _db.AppSettings.Add(Settings);
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(Settings);
            }
            await _db.SaveChangesAsync();
            SaveStatus = "Settings saved.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        Settings = new AppSettings();
        SaveStatus = "Defaults restored (not saved).";
    }
}
