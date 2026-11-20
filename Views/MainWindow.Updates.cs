using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LootDrop.Services;

namespace LootDrop.Views;
public partial class MainWindow
{
    private UpdateService _updateService = new();
    private UpdateRelease? _availableUpdate;
    private CancellationTokenSource? _updateWork;
    private void InitializeUpdates()
    {
        InstalledVersionText.Text="Installed version: " + UpdateService.CurrentVersionText;
        IncludePrereleasesCheck.PropertyChanged += (_,e) =>
        {
            if(e.Property!=CheckBox.IsCheckedProperty) return;
            _updateWork?.Cancel();ClearUpdateResult();UpdateStatusText.Text="Channel changed. Check again to refresh available downloads.";
        };
        Closed += (_,_) => _updateWork?.Cancel();
    }
    private void ClearUpdateResult()
    {
        _availableUpdate=null;UpdatePackages.ItemsSource=null;UpdateNotesText.Text="";DownloadUpdateButton.IsEnabled=false;ReleaseNotesButton.IsEnabled=false;
    }
    public async Task CheckForUpdatesAsync()
    {
        if(_updateWork!=null) return;
        ClearUpdateResult();_updateWork=new CancellationTokenSource();
        CheckUpdatesButton.IsEnabled=false;IncludePrereleasesCheck.IsEnabled=false;CancelUpdateButton.IsEnabled=true;
        UpdateStatusText.Text="Checking public releases on GitHub…";
        try
        {
            var release=await _updateService.CheckAsync(IncludePrereleasesCheck.IsChecked==true,_updateWork.Token);
            _updateWork.Token.ThrowIfCancellationRequested();
            if(release==null) { UpdateStatusText.Text="No supported release found for this channel. Enable tester prereleases to include test builds.";return; }
            _availableUpdate=release;ReleaseNotesButton.IsEnabled=true;UpdateNotesText.Text=release.Notes;
            if(!UpdateService.IsNewer(release,UpdateService.CurrentVersion))
            {UpdateStatusText.Text=$"No newer version found. Installed: {UpdateService.CurrentVersionText}; available: {release.Tag}{(release.Prerelease?" (tester prerelease)":"")}.";return;}
            UpdateStatusText.Text=$"Update available: {release.Tag}{(release.Prerelease?" (tester prerelease)":"")}. Choose a package below. Downloads are not installed automatically.";
            UpdatePackages.ItemsSource=release.Assets.Select(a=>a.Name).ToArray();UpdatePackages.SelectedIndex=release.Assets.Count>0?0:-1;
            DownloadUpdateButton.IsEnabled=release.Assets.Count>0;
            if(release.Assets.Count==0) UpdateStatusText.Text+=" No supported package with a SHA256 digest is available; see the release page.";
        }
        catch(OperationCanceledException){ClearUpdateResult();UpdateStatusText.Text="Update check cancelled or timed out. Try again when connected.";}
        catch(Exception){ClearUpdateResult();UpdateStatusText.Text="Could not check updates. GitHub may be unavailable, rate-limited, or returned unsupported data. Try again later.";}
        finally{_updateWork.Dispose();_updateWork=null;CheckUpdatesButton.IsEnabled=true;IncludePrereleasesCheck.IsEnabled=true;CancelUpdateButton.IsEnabled=false;}
    }
    private async void CheckUpdates_Click(object? sender,RoutedEventArgs e)=>await CheckForUpdatesAsync();
    private void CancelUpdate_Click(object? sender,RoutedEventArgs e)=>_updateWork?.Cancel();
    private async void DownloadUpdate_Click(object? sender,RoutedEventArgs e)
    {
        if(_updateWork!=null || _availableUpdate==null || !UpdateService.IsNewer(_availableUpdate,UpdateService.CurrentVersion) || UpdatePackages.SelectedItem is not string selected) return;
        var release=_availableUpdate;var asset=release.Assets.SingleOrDefault(a=>a.Name==selected);if(asset==null)return;
        _updateWork=new CancellationTokenSource();CheckUpdatesButton.IsEnabled=false;IncludePrereleasesCheck.IsEnabled=false;DownloadUpdateButton.IsEnabled=false;CancelUpdateButton.IsEnabled=true;
        try
        {
            var folders=await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {Title="Save unsigned update — choose a local folder",AllowMultiple=false});
            if(folders.Count==0){UpdateStatusText.Text="Download cancelled. Nothing was installed.";return;}
            _updateWork.Token.ThrowIfCancellationRequested();
            var folder=folders[0].TryGetLocalPath() ?? throw new InvalidOperationException();
            var progress=new Progress<int>(p=> {if(_updateWork!=null)UpdateStatusText.Text=$"Downloading {asset.Name}: {p}% · Will verify SHA256 before saving.";});
            var path=await _updateService.DownloadAsync(release,asset,folder,_updateWork.Token,progress);
            UpdateStatusText.Text="Saved: " + path + "\nSHA256 matches GitHub metadata. This is not a publisher signature. Close LootDrop before manually installing/updating; back up your library first. Nothing was launched.";
        }
        catch(OperationCanceledException){UpdateStatusText.Text="Download cancelled or timed out. The incomplete file was removed.";}
        catch(Exception){UpdateStatusText.Text="Download failed. Choose a writable local folder without an existing copy; check your connection and try again. Integrity failures are never installed.";}
        finally{_updateWork.Dispose();_updateWork=null;CheckUpdatesButton.IsEnabled=true;IncludePrereleasesCheck.IsEnabled=true;CancelUpdateButton.IsEnabled=false;DownloadUpdateButton.IsEnabled=_availableUpdate==release && release.Assets.Count>0;}
    }
    private void UpdateReleaseNotes_Click(object? sender,RoutedEventArgs e)
    {
        if(_availableUpdate==null)return;
        // Reconstruct the fixed repository address rather than launching arbitrary metadata.
        var url=UpdateService.ReleasesUrl + "/tag/" + _availableUpdate.Tag;
        try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception){UpdateStatusText.Text="Could not open the release page. Visit github.com/remicks/LootDrop-Windows/releases manually.";}
    }
}
