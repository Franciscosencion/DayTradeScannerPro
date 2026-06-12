using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Interfaces;
using TradeScanner.UI.Infrastructure;

namespace TradeScanner.UI.ViewModels.Providers;

public partial class ProvidersViewModel : ViewModelBase
{
    private readonly IProviderFactory _providerFactory;
    private readonly IProviderConfigService _configService;

    [ObservableProperty] private ProviderRow? _selectedProvider;

    public ObservableCollection<ProviderRow> Providers { get; } = [];

    public ProvidersViewModel(IProviderFactory providerFactory, IProviderConfigService configService)
    {
        _providerFactory = providerFactory;
        _configService = configService;
    }

    public async Task LoadAsync()
    {
        Providers.Clear();

        // Build rows from live providers
        var rows = _providerFactory.GetAllProviders()
            .Select(p => new ProviderRow
            {
                ProviderType = p.ProviderType,
                DisplayName = p.DisplayName,
                Priority = p.Priority,
                IsAvailable = p.IsAvailable,
                ApiKey = string.Empty
            })
            .ToDictionary(r => r.ProviderType);

        // Fill in saved keys (decrypted display is not needed — just show masked indicator)
        var configs = await _configService.GetAllAsync();
        foreach (var cfg in configs)
        {
            if (rows.TryGetValue(cfg.Provider, out var row) && !string.IsNullOrEmpty(cfg.EncryptedApiKey))
                row.HasSavedKey = true;
        }

        foreach (var row in rows.Values.OrderBy(r => r.Priority))
            Providers.Add(row);
    }

    [RelayCommand]
    private async Task SaveApiKeyAsync(ProviderRow row)
    {
        if (string.IsNullOrWhiteSpace(row.ApiKey)) return;
        IsBusy = true;
        try
        {
            await _configService.SaveKeyAsync(row.ProviderType, row.ApiKey);
            row.HasSavedKey = true;
            row.IsAvailable = true;
            row.ApiKey = string.Empty;       // clear the field after save (don't show key in UI)
            row.ValidationStatus = "Saved";
            StatusMessage = $"{row.DisplayName}: API key saved";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClearApiKeyAsync(ProviderRow row)
    {
        IsBusy = true;
        try
        {
            await _configService.SaveKeyAsync(row.ProviderType, string.Empty);
            row.HasSavedKey = false;
            row.IsAvailable = false;
            row.ValidationStatus = string.Empty;
            StatusMessage = $"{row.DisplayName}: API key cleared";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ValidateProviderAsync(ProviderRow row)
    {
        IsBusy = true;
        try
        {
            // If a new key is entered, save it first before validating
            if (!string.IsNullOrWhiteSpace(row.ApiKey))
                await _configService.SaveKeyAsync(row.ProviderType, row.ApiKey);

            var provider = _providerFactory.GetProvider(row.ProviderType);
            var isValid = await provider.ValidateApiKeyAsync();
            row.ValidationStatus = isValid ? "Valid" : "Invalid";
            row.IsAvailable = isValid;

            if (isValid && !string.IsNullOrWhiteSpace(row.ApiKey))
            {
                row.HasSavedKey = true;
                row.ApiKey = string.Empty;
            }

            StatusMessage = $"{row.DisplayName}: {row.ValidationStatus}";
        }
        finally { IsBusy = false; }
    }
}

public partial class ProviderRow : ObservableObject
{
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private MarketDataProvider _providerType;
    [ObservableProperty] private int _priority;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _hasSavedKey;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _validationStatus = string.Empty;
}
